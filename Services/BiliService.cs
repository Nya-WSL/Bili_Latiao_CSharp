using QRCoder;
using Bili_Latiao_CSharp.Models;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bili_Latiao_CSharp.Services
{
    public enum QrCodePollStatus
    {
        Success,
        Waiting,
        Scanned,
        Expired,
        Error
    }

    public class BiliPollError(JsonElement info) : Exception($"扫码出现错误：{info.GetProperty("data").GetProperty("message").GetString()}")
    {
        public JsonElement Info { get; } = info;
    }

    public class BiliService
    {
        private static readonly string User_Agent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";
        private static readonly string wbi_url = "https://api.bilibili.com/x/web-interface/nav";
        private static readonly string buvid3_url = "https://api.bilibili.com/x/web-frontend/getbuvid";
        private static readonly string uname_url = "https://api.bilibili.com/x/web-interface/card";
        private static readonly string generate_qrcode_url = "https://passport.bilibili.com/x/passport-login/web/qrcode/generate";
        private static readonly string poll_qrcode_url = "https://passport.bilibili.com/x/passport-login/web/qrcode/poll";
        private static readonly string room_info_url = "https://api.live.bilibili.com/room/v1/Room/get_info";
        private static readonly string gift_send_url = "https://api.live.bilibili.com/gift/v2/gift/send";
        private static readonly string like_report_url = "https://api.live.bilibili.com/xlive/app-ucenter/v1/like_info_v3/like/likeReportV3";

        private static readonly int[] mixinKeyEncTab = {
            46, 47, 18, 2, 53, 8, 23, 32, 15, 50, 10, 31, 58, 3, 45, 35, 27, 43, 5, 49,
            33, 9, 42, 19, 29, 28, 14, 39, 12, 38, 41, 13, 37, 48, 7, 16, 24, 55, 40,
            61, 26, 17, 0, 1, 60, 51, 30, 4, 22, 25, 54, 21, 56, 59, 6, 63, 57, 62, 11,
            36, 20, 34, 44, 52
        };

        private readonly HttpClient _httpClient;

        public BiliService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            // 设置默认User-Agent
            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(User_Agent);
            }
        }

        private static string GetMixinKey(string orig)
        {
            // 对 imgKey 和 subKey 进行字符顺序打乱编码
            var result = new StringBuilder();
            foreach (var i in mixinKeyEncTab)
            {
                if (i < orig.Length)
                    result.Append(orig[i]);
            }
            return result.ToString()[..Math.Min(32, result.Length)];
        }

        public async Task<Dictionary<string, string>> EncWbiAsync(Dictionary<string, string> parameters)
        {
            // 为请求参数进行 wbi 签名
            try
            {
                var (imgKey, subKey) = await GetWbiKeysAsync();
                var mixinKey = GetMixinKey(imgKey + subKey);
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var paramsCopy = new Dictionary<string, string>(parameters)
                {
                    ["wts"] = currentTime.ToString()
                };

                // 按照 key 排序
                var sortedParams = paramsCopy.OrderBy(kv => kv.Key)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                // 过滤 value 中的 "!'()*" 字符
                var filteredParams = new Dictionary<string, string>();
                foreach (var kv in sortedParams)
                {
                    var filteredValue = Regex.Replace(kv.Value, @"[!'()*]", "");
                    filteredParams[kv.Key] = filteredValue;
                }

                // 序列化参数
                var query = string.Join("&", filteredParams.Select(kv =>
                    $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

                // 计算 w_rid
                using var md5 = MD5.Create();
                var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(query + mixinKey));
                var wbiSign = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                filteredParams["w_rid"] = wbiSign;
                return filteredParams;
            }
            catch (Exception e)
            {
                LogService.Error(e, "WBI签名失败");
                throw;
            }
        }

        public async Task<(string imgKey, string subKey)> GetWbiKeysAsync()
        {
            // 获取最新的 img_key 和 sub_key
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, wbi_url);
                request.Headers.Referrer = new Uri("https://www.bilibili.com/");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadFromJsonAsync<JsonElement>();

                if (jsonContent.GetProperty("code").GetInt32() != 0)
                {
                    // 未登录时 code=-101，但 wbi_img 数据仍然可用
                    if (!jsonContent.TryGetProperty("data", out var d) ||
                        !d.TryGetProperty("wbi_img", out var _))
                    {
                        var errorMsg = $"获取WBI密钥失败: {jsonContent}";
                        LogService.Error(errorMsg);
                        throw new Exception(errorMsg);
                    }
                    LogService.Info("code!=0 但 wbi_img 存在，继续提取密钥");
                }

                var wbiImg = jsonContent.GetProperty("data").GetProperty("wbi_img");

                var imgUrl = wbiImg.GetProperty("img_url").GetString();
                var subUrl = wbiImg.GetProperty("sub_url").GetString();

                var imgKey = imgUrl.Split('/').Last().Split('.').First();
                var subKey = subUrl.Split('/').Last().Split('.').First();

                return (imgKey, subKey);
            }
            catch (Exception e)
            {
                LogService.Error(e, "获取WBI密钥异常");
                throw;
            }
        }

        public async Task<Dictionary<string, string>> WbiSignAsync()
        {
            try
            {
                var signedParams = await EncWbiAsync(new Dictionary<string, string>
                {
                    ["foo"] = "114",
                    ["bar"] = "514",
                    ["baz"] = "1919810"
                });

                // 移除参数
                signedParams.Remove("foo");
                signedParams.Remove("bar");
                signedParams.Remove("baz");

                return signedParams;
            }
            catch (Exception e)
            {
                LogService.Error(e, "生成WBI签名参数失败");
                throw;
            }
        }

        public void GenerateQrImage(string text, string filePath)
        {
            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(20);

                File.WriteAllBytes(filePath, qrCodeImage);

            }
            catch (Exception e)
            {
                LogService.Error(e, "生成二维码图片失败");
                throw;
            }
        }

        public async Task<string> GetQrcodeAsync()
        {
            // 获取B站Web端扫码登录二维码
            try
            {
                var response = await _httpClient.GetAsync(generate_qrcode_url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var data = json.GetProperty("data");
                var url = data.GetProperty("url").GetString();
                var qrcodeKey = data.GetProperty("qrcode_key").GetString();

                // 生成二维码图片
                GenerateQrImage(url, "login.png");

                return qrcodeKey;
            }
            catch (Exception e)
            {
                LogService.Error(e, "获取二维码失败");
                throw;
            }
        }

        public async Task<string?> GetBuvid3Async()
        {
            try
            {
                var response = await _httpClient.GetAsync(buvid3_url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    var code = json.GetProperty("code").GetInt32();

                    if (code == 0)
                    {
                        var buvid3 = json.GetProperty("data").GetProperty("buvid").GetString();
                        return buvid3;
                    }
                    else
                    {
                        var errorMsg = $"获取buvid3失败，错误信息：{code} {json.GetProperty("message").GetString()}";
                        LogService.Error(errorMsg);
                    }
                }
                else
                {
                    LogService.Error($"获取buvid3失败，HTTP状态码：{response.StatusCode}");
                }
            }
            catch (Exception e)
            {
                LogService.Error(e, "获取buvid3异常");
            }

            return null;
        }

        public async Task<string?> GetUnameAsync(string mid)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{uname_url}?mid={mid}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    var code = json.GetProperty("code").GetInt32();

                    if (code == 0)
                    {
                        var uname = json.GetProperty("data").GetProperty("card").GetProperty("name").GetString();
                        return uname;
                    }
                    else
                    {
                        var errorMsg = $"获取用户昵称失败，错误信息：{code} {json.GetProperty("message").GetString()}";
                        LogService.Error(errorMsg);
                    }
                }
                else
                {
                    LogService.Error($"获取用户昵称失败，HTTP状态码：{response.StatusCode}");
                }
            }
            catch (Exception e)
            {
                LogService.Error(e, "获取用户昵称异常");
            }

            return null;
        }

        public async Task<JsonElement> GetUidAsync(string roomId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{room_info_url}?room_id={roomId}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var code = json.GetProperty("code").GetInt32();

                if (code != 0)
                {
                    var errorMsg = $"获取UID失败，错误代码: {code}，响应: {json}";
                    LogService.Error(errorMsg);
                    throw new Exception(errorMsg);
                }

                return json;
            }
            catch (Exception e)
            {
                LogService.Error(e, "获取UID失败");
                throw;
            }
        }

        public async Task<QrCodePollStatus> LoginAsync(string qrcodeKey)
        {
            try
            {
                var buvid3 = await GetBuvid3Async();

                if (string.IsNullOrEmpty(buvid3))
                {
                    LogService.Error("获取buvid3失败，无法登录");
                    return QrCodePollStatus.Error;
                }

                var response = await _httpClient.GetAsync($"{poll_qrcode_url}?qrcode_key={qrcodeKey}");
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var data = json.GetProperty("data");
                var code = data.GetProperty("code").GetInt32();

                switch (code)
                {
                    case 0:
                        LogService.Info("登录成功");
                        break;
                    case 86101:
                        LogService.Info("等待扫码...");
                        return QrCodePollStatus.Waiting;
                    case 86090:
                        LogService.Info("已扫码，等待确认...");
                        return QrCodePollStatus.Scanned;
                    case 86038:
                        LogService.Info("二维码已过期");
                        return QrCodePollStatus.Expired;
                    default:
                        var error = new BiliPollError(json);
                        LogService.Error(error, "登录失败");
                        return QrCodePollStatus.Error;
                }

                // 获取cookies，更新AppConfig
                if (response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
                {
                    var config = ConfigService.Load();
                    config.Buvid3 = buvid3;

                    foreach (var cookie in cookieHeaders)
                    {
                        var cookieParts = cookie.Split(';')[0].Split('=');
                        if (cookieParts.Length != 2) continue;

                        var name = cookieParts[0].Trim();
                        var value = cookieParts[1].Trim();

                        switch (name)
                        {
                            case "SESSDATA": config.SessData = value; break;
                            case "bili_jct": config.BiliJct = value; break;
                            case "DedeUserID": config.DedeUserId = value; break;
                        }
                    }

                    if (!string.IsNullOrEmpty(config.DedeUserId))
                    {
                        var uname = await GetUnameAsync(config.DedeUserId);
                        if (!string.IsNullOrEmpty(uname))
                        {
                            config.Uname = uname;
                        }
                    }

                    ConfigService.Save(config);
                    return QrCodePollStatus.Success;
                }
                else
                {
                    LogService.Error("登录响应中没有找到Cookie");
                    return QrCodePollStatus.Error;
                }
            }
            catch (Exception e)
            {
                LogService.Error(e, "登录流程异常");
                throw;
            }
        }

        /// <summary>
        /// 赠送辣条
        /// </summary>
        public async Task<string> SendLatiaoAsync(AppConfig config, int num)
        {
            try
            {
                LogService.Info($"开始赠送 {num} 个辣条");

                var roomInfo = await GetUidAsync(config.RoomId.ToString());
                var ruid = roomInfo.GetProperty("data").GetProperty("uid").GetInt64();

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["gift_id"] = "1",
                    ["gift_num"] = num.ToString(),
                    ["ruid"] = ruid.ToString(),
                    ["coin_type"] = "silver",
                    ["biz_id"] = config.RoomId.ToString(),
                    ["csrf"] = config.BiliJct,
                    ["csrf_token"] = config.BiliJct,
                });

                var request = new HttpRequestMessage(HttpMethod.Post, gift_send_url) { Content = content };
                request.Headers.Referrer = new Uri($"https://live.bilibili.com/{config.RoomId}");
                request.Headers.TryAddWithoutValidation("Cookie",
                    $"SESSDATA={config.SessData}; bili_jct={config.BiliJct}");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var code = json.GetProperty("code").GetInt32();

                if (code == 0)
                {
                    var data = json.GetProperty("data");
                    var msg = $"{data.GetProperty("uname").GetString()} " +
                              $"在 {roomInfo.GetProperty("data").GetProperty("room_id").GetInt64()} " +
                              $"{data.GetProperty("gift_action").GetString()} " +
                              $"{data.GetProperty("gift_num").GetInt32()} 个 " +
                              $"{data.GetProperty("gift_name").GetString()}";
                    LogService.Info(msg);
                    return msg;
                }
                else
                {
                    var errMsg = json.TryGetProperty("message", out var m) ? m.GetString() : "未知错误";
                    LogService.Error($"赠送辣条失败: {errMsg}");
                    return $"赠送失败: {errMsg}";
                }
            }
            catch (Exception e)
            {
                LogService.Error(e, "赠送辣条异常");
                throw;
            }
        }

        /// <summary>
        /// 点赞（一次点满 或 手动模拟）
        /// </summary>
        public async Task<string> LikeReportAsync(AppConfig config, int likeNum, bool once, Action<string>? onLog = null, CancellationToken token = default)
        {
            try
            {
                LogService.Info($"开始点赞，数量: {likeNum}, 模式: {(once ? "一次点满" : "手动模拟")}");

                var roomInfo = await GetUidAsync(config.RoomId.ToString());
                var anchorId = roomInfo.GetProperty("data").GetProperty("uid").GetInt64();

                if (string.IsNullOrEmpty(config.BiliJct))
                    throw new Exception("未登录或缺少 bili_jct");

                var wbiParams = await WbiSignAsync();

                if (likeNum > 1000)
                {
                    LogService.Info("点赞数上限1000，超过部分将不会计算");
                    likeNum = 1000;
                }

                if (once)
                {
                    var allParams = new Dictionary<string, string>(wbiParams)
                    {
                        ["click_time"] = likeNum.ToString(),
                        ["room_id"] = config.RoomId.ToString(),
                        ["uid"] = config.DedeUserId,
                        ["anchor_id"] = anchorId.ToString(),
                        ["csrf"] = config.BiliJct,
                    };

                    var queryStr = string.Join("&",
                        allParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

                    var request = new HttpRequestMessage(HttpMethod.Post, $"{like_report_url}?{queryStr}");
                    request.Headers.TryAddWithoutValidation("Cookie",
                        $"buvid3={config.Buvid3}; SESSDATA={config.SessData}; bili_jct={config.BiliJct}");
                    request.Headers.Referrer = new Uri($"https://live.bilibili.com/{config.RoomId}");

                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    var code = json.GetProperty("code").GetInt32();

                    if (code == 0)
                    {
                        LogService.Info("点赞成功");
                        return "点赞成功";
                    }
                    else
                    {
                        var errMsg = json.TryGetProperty("message", out var m) ? m.GetString() : "未知错误";
                        LogService.Error($"点赞失败: code={code}, msg={errMsg}");
                        return $"点赞失败: {errMsg}";
                    }
                }
                else
                {
                    // 手动模拟：每2秒随机点赞50-100个
                    var random = new Random();
                    var total = 0;
                    var times = 0;

                    while (likeNum > 0 && !token.IsCancellationRequested)
                    {
                        var clickTime = likeNum > 50
                            ? random.Next(50, Math.Min(101, likeNum + 1))
                            : likeNum;
                        likeNum -= clickTime;

                        var allParams = new Dictionary<string, string>(wbiParams)
                        {
                            ["click_time"] = clickTime.ToString(),
                            ["room_id"] = config.RoomId.ToString(),
                            ["uid"] = config.DedeUserId,
                            ["anchor_id"] = anchorId.ToString(),
                            ["csrf"] = config.BiliJct,
                        };

                        var queryStr = string.Join("&",
                            allParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

                        var request = new HttpRequestMessage(HttpMethod.Post, $"{like_report_url}?{queryStr}");
                        request.Headers.TryAddWithoutValidation("Cookie",
                            $"buvid3={config.Buvid3}; SESSDATA={config.SessData}; bili_jct={config.BiliJct}");
                        request.Headers.Referrer = new Uri($"https://live.bilibili.com/{config.RoomId}");

                        var response = await _httpClient.SendAsync(request);
                        times++;

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                            var code = json.GetProperty("code").GetInt32();
                            if (code == 0)
                            {
                                total += clickTime;
                                var msg = $"第 {times} 次点赞成功，点赞数：{clickTime}，总数：{total}";
                                LogService.Info(msg);
                                onLog?.Invoke(msg);
                            }
                            else
                            {
                                var errMsg = json.TryGetProperty("message", out var m) ? m.GetString() : "未知错误";
                                LogService.Error($"第 {times} 次点赞失败：{errMsg}");
                                onLog?.Invoke($"第 {times} 次点赞失败：{errMsg}");
                            }
                        }
                        else
                        {
                            LogService.Error($"第 {times} 次请求失败，错误码：{response.StatusCode}");
                            onLog?.Invoke($"第 {times} 次请求失败，错误码：{response.StatusCode}");
                        }

                        if (likeNum > 0 && !token.IsCancellationRequested)
                            await Task.Delay(2000, token);
                    }

                    var resultMsg = $"点赞完成，次数：{times}，总数：{total}";
                    LogService.Info(resultMsg);
                    return resultMsg;
                }
            }
            catch (TaskCanceledException)
            {
                LogService.Info("点赞操作已取消");
                return "操作已取消";
            }
            catch (Exception e)
            {
                LogService.Error(e, "点赞异常");
                throw;
            }
        }
    }
}