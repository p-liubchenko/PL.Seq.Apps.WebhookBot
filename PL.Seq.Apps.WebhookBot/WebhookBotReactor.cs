using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Seq.Apps;
using Seq.Apps.LogEvents;

namespace PL.Seq.Apps.WebhookBot
{
    [SeqApp(
        "Webhook Bot Notifier",
        Description = "Sends Seq alert/event notifications to a Teams Webhook Bot. " +
                      "The message body is rendered from a Liquid template with full " +
                      "access to the alert and event context.")]
    public class WebhookBotReactor : SeqApp, ISubscribeToAsync<LogEventData>
    {
        // One HttpClient for the lifetime of the app instance (avoids socket exhaustion).
        private static readonly HttpClient Http = new HttpClient();

        [SeqAppSetting(
            DisplayName = "Webhook URL",
            IsOptional = false,
            HelpText = "Full webhook bot URL, e.g. " +
                       "https://webhookbot.c-toss.com/api/bot/webhooks/00000000-0000-0000-0000-000000000000")]
        public string WebhookUrl { get; set; }

        [SeqAppSetting(
            DisplayName = "Message template (Liquid)",
            IsOptional = false,
            InputType = SettingInputType.LongText,
            // Invocation parameter: editable per-alert in the alert's notification
            // ("customize notification") settings, so one app instance can serve
            // many alerts, each with its own message.
            IsInvocationParameter = true,
            HelpText = "Liquid template for the message text. Event properties are available " +
                       "as top-level variables (e.g. {{ count }}), all properties under " +
                       "{{ properties.* }}, and event metadata under {{ evt.Message }}, " +
                       "{{ evt.Level }}, {{ evt.Timestamp }}, etc.")]
        public string MessageTemplate { get; set; }

        [SeqAppSetting(
            DisplayName = "Debug logging",
            IsOptional = true,
            InputType = SettingInputType.Checkbox,
            HelpText = "Log the available template variables and the rendered message. " +
                       "Useful when working out property names for a new alert.")]
        public bool DebugLog { get; set; }

        public async Task OnAsync(Event<LogEventData> evt)
        {
            var model = EventModel.Build(evt);

            if (DebugLog)
                Log.Debug("WebhookBot: available template variables: {Variables}", string.Join(", ", model.Keys));

            if (!LiquidRenderer.TryRender(MessageTemplate, model, out var text, out var renderError))
            {
                Log.Error("WebhookBot: failed to render Liquid template: {RenderError}", renderError);
                return;
            }

            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                if (DebugLog)
                    Log.Debug("WebhookBot: rendered message is empty, skipping send for event {EventId}", evt.Id);
                return;
            }

            string payload = JsonConvert.SerializeObject(new { text });

            using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
            using (var response = await Http.PostAsync(WebhookUrl, content).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Error(
                        "WebhookBot: POST to webhook failed with {StatusCode} {ReasonPhrase}: {Body}",
                        (int)response.StatusCode, response.ReasonPhrase, body);
                }
                else if (DebugLog)
                {
                    Log.Debug("WebhookBot: sent notification for event {EventId}: {Text}", evt.Id, text);
                }
            }
        }
    }
}
