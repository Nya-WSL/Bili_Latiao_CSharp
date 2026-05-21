using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace Bili_Latiao_CSharp.Models
{
    public partial class AppConfig : ObservableObject
    {
        [ObservableProperty]
        [property: JsonPropertyName("uname")]
        private string _uname = "";

        [ObservableProperty]
        [property: JsonPropertyName("room_id")]
        private double _roomId = 31842;

        [ObservableProperty]
        [property: JsonPropertyName("SESSDATA")]
        private string _sessData = "";

        [ObservableProperty]
        [property: JsonPropertyName("bili_jct")]
        private string _biliJct = "";

        [ObservableProperty]
        [property: JsonPropertyName("DedeUserID")]
        private string _dedeUserId = "";

        [ObservableProperty]
        [property: JsonPropertyName("buvid3")]
        private string _buvid3 = "";
    }
}