using Seq.Apps;

using System;
using System.Threading.Tasks;

namespace PL.Seq.Apps.WebhookBot
{
    [SeqApp("WebhookBot Link Notifier", Description = "Uses a Handlebars template to send events as DingTalk For Link.")]
    public class WebhookBotReactor : SeqApp, ISubscribeToJsonAsync
    {
        [SeqAppSetting(DisplayName = "Url", IsOptional = false)]
        public string Url { get; set; }

        [SeqAppSetting(DisplayName = "BaseUrl", IsOptional = true)]
        public string BaseUrl { get; set; } = null;

        [SeqAppSetting(DisplayName = "Debug Log", IsOptional = true, InputType = SettingInputType.Checkbox)]
        public bool? DebugLog { get; set; }

        public async Task OnAsync(string json)
        {
            if (DebugLog == true) this.Log.Debug("[DebugLog] {AppTitle};InstanceName:{InstanceName};BaseUri:{BaseUri};json:{json}", App.Title, Host.InstanceName, Host.BaseUri, json);
            if (!string.IsNullOrWhiteSpace(this.Url))
            {
                using (var http = new System.Net.Http.HttpClient())
                {
                    var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    content.Headers.Add("Seq_Title", App.Title);
                    content.Headers.Add("Seq_InstanceName", Host.InstanceName);
                    content.Headers.Add("Seq_BaseUri", BaseUrl ?? Host.BaseUri);

                    var r = await http.SendAsync(new System.Net.Http.HttpRequestMessage()
                    {
                        RequestUri = new Uri(this.Url),
                        Method = System.Net.Http.HttpMethod.Post,
                        Content = content
                    });
                    var d = await r.Content.ReadAsStringAsync();
                    if (!r.IsSuccessStatusCode)
                    {
                        this.Log.Error("http，Error:" + d);
                    }
                }
            }
        }
    }
}
