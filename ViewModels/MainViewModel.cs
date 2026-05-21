using Bili_Latiao_CSharp.Models;
using Bili_Latiao_CSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using static Bili_Latiao_CSharp.Services.QrCodePollStatus;

namespace Bili_Latiao_CSharp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // ── 服务 ──
        private readonly BiliService _biliService;
        private AppConfig _config;
        private CancellationTokenSource? _cts;

        // ── 属性 ──

        private const string AppVersion = "v1.0.0";

        [ObservableProperty]
        private string _title = $"B站辣条姬 {AppVersion}";

        [ObservableProperty]
        private string _userName = "未登录";

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private string _qrcodeKey = "";

        [ObservableProperty]
        private bool _isLoggingIn;

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private double _roomId = 31842;

        [ObservableProperty]
        private System.Windows.Media.ImageSource? _qrcodeImage;

        [ObservableProperty]
        private int _latiaoNum = 1;

        [ObservableProperty]
        private int _likeNum = 1000;

        [ObservableProperty]
        private bool _isOperating;

        // ── 日志 ──
        public ObservableCollection<string> LogMessages { get; } = [];

        // ── 配置公开 ──
        public AppConfig Config => _config;

        public MainViewModel()
        {
            _config = ConfigService.Load();
            _biliService = new BiliService(new HttpClient());
            RoomId = _config.RoomId;

            if (!string.IsNullOrEmpty(_config.Uname))
            {
                UserName = _config.Uname;
                IsLoggedIn = true;
            }
        }

        // ── 命令 ──

        [RelayCommand]
        private async Task LoginAsync()
        {
            if (IsLoggingIn) return;
            IsLoggingIn = true;
            StatusMessage = "正在获取登录二维码...";

            try
            {
                QrcodeKey = await _biliService.GetQrcodeAsync();
                LoadQrcodeImage("login.png");
                StatusMessage = "请使用B站APP扫描二维码登录";

                // 轮询扫码结果
                while (IsLoggingIn)
                {
                    await Task.Delay(2000);
                    var status = await _biliService.LoginAsync(QrcodeKey);

                    switch (status)
                    {
                        case Success:
                            IsLoggedIn = true;
                            var newConfig = ConfigService.Load();
                            UserName = newConfig.Uname;
                            StatusMessage = $"已登录：{UserName}";
                            IsLoggingIn = false;
                            return;

                        case Scanned:
                            StatusMessage = "已扫码，请在手机上确认...";
                            break;

                        case Expired:
                            StatusMessage = "二维码已过期，请重新扫码";
                            AddLog("二维码已过期");
                            IsLoggingIn = false;
                            return;

                        case Error:
                            StatusMessage = "登录出错，请重试";
                            AddLog("登录出错");
                            IsLoggingIn = false;
                            return;

                        case Waiting:
                        default:
                            // 继续等待
                            break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"登录失败：{ex.Message}";
                AddLog($"登录失败：{ex.Message}");
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        [RelayCommand]
        private void CancelLogin()
        {
            IsLoggingIn = false;
            StatusMessage = "已取消登录";
        }

        [RelayCommand]
        private async Task GetRoomInfoAsync()
        {
            StatusMessage = $"正在获取房间 {RoomId} 信息...";
            try
            {
                var json = await _biliService.GetUidAsync(RoomId.ToString());
                StatusMessage = "获取房间信息成功";
                AddLog($"房间 {RoomId} 信息获取成功");
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"获取房间信息失败：{ex.Message}";
                AddLog($"获取房间信息失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SendLatiaoAsync()
        {
            if (!CheckLogin()) return;
            if (IsOperating) return;

            IsOperating = true;
            _cts = new CancellationTokenSource();
            StatusMessage = $"正在赠送 {LatiaoNum} 个辣条...";

            try
            {
                var result = await _biliService.SendLatiaoAsync(_config, LatiaoNum);
                AddLog(result);
                StatusMessage = "赠送完成";
            }
            catch (System.Exception ex)
            {
                AddLog($"赠送辣条失败：{ex.Message}");
                StatusMessage = $"赠送失败：{ex.Message}";
            }
            finally
            {
                IsOperating = false;
            }
        }

        [RelayCommand]
        private async Task SendLatiaoLoopAsync()
        {
            if (!CheckLogin()) return;
            if (IsOperating) return;

            IsOperating = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                var count = LatiaoNum;
                var sent = 0;

                while (count > 0 && !token.IsCancellationRequested)
                {
                    var result = await _biliService.SendLatiaoAsync(_config, 1);
                    AddLog(result);
                    sent++;
                    count--;

                    if (count > 0 && !token.IsCancellationRequested)
                        await Task.Delay(2000, token);
                }

                StatusMessage = $"循环赠送完成，共赠送 {sent} 个辣条";
            }
            catch (TaskCanceledException)
            {
                StatusMessage = "操作已停止";
            }
            catch (System.Exception ex)
            {
                AddLog($"循环赠送失败：{ex.Message}");
                StatusMessage = $"赠送失败：{ex.Message}";
            }
            finally
            {
                IsOperating = false;
            }
        }

        [RelayCommand]
        private async Task LikeOnceAsync()
        {
            if (!CheckLogin()) return;
            if (IsOperating) return;

            IsOperating = true;
            StatusMessage = $"正在一次点满 {LikeNum} 个赞...";

            try
            {
                var result = await _biliService.LikeReportAsync(_config, LikeNum, true);
                AddLog(result);
                StatusMessage = result;
            }
            catch (System.Exception ex)
            {
                AddLog($"点赞失败：{ex.Message}");
                StatusMessage = $"点赞失败：{ex.Message}";
            }
            finally
            {
                IsOperating = false;
            }
        }

        [RelayCommand]
        private async Task LikeSimulateAsync()
        {
            if (!CheckLogin()) return;
            if (IsOperating) return;

            IsOperating = true;
            _cts = new CancellationTokenSource();

            try
            {
                var result = await _biliService.LikeReportAsync(_config, LikeNum, false, AddLog, _cts.Token);
                AddLog(result);
                StatusMessage = result;
            }
            catch (TaskCanceledException)
            {
                StatusMessage = "操作已停止";
            }
            catch (System.Exception ex)
            {
                AddLog($"点赞失败：{ex.Message}");
                StatusMessage = $"点赞失败：{ex.Message}";
            }
            finally
            {
                IsOperating = false;
            }
        }

        [RelayCommand]
        private void StopOperation()
        {
            _cts?.Cancel();
            IsOperating = false;
            StatusMessage = "正在停止操作...";
            AddLog("已停止操作");
        }

        [RelayCommand]
        private void DragMove(Window window)
        {
            window.DragMove();
        }

        [RelayCommand]
        private void Close(Window window)
        {
            window.Close();
        }

        // 房间号变更时同步到配置，触发自动保存
        partial void OnRoomIdChanged(double value)
        {
            if (_config != null)
            {
                _config.RoomId = value;
            }
        }

        // ── 辅助 ──

        private bool CheckLogin()
        {
            _config = ConfigService.Load();
            if (string.IsNullOrEmpty(_config.Uname))
            {
                StatusMessage = "未登录B站账号，请先登录";
                AddLog("未登录，请先扫码登录");
                return false;
            }
            return true;
        }

        private void LoadQrcodeImage(string filePath)
        {
            var fullPath = System.IO.Path.GetFullPath(filePath);
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(fullPath);
            bmp.EndInit();
            bmp.Freeze(); // 允许跨线程访问
            QrcodeImage = bmp;
        }

        public void AddLog(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogMessages.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            });
        }
    }
}
