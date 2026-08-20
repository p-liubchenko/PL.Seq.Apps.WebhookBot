using System;
using System.Collections.Generic;

using Fluid;

namespace PL.Seq.Apps.WebhookBot
{
    /// <summary>
    /// Thin wrapper around the Fluid Liquid engine. Every entry in the supplied
    /// model is registered as a top-level template variable.
    /// </summary>
    internal static class LiquidRenderer
    {
        private static readonly FluidParser Parser = new FluidParser();

        public static bool TryRender(
            string template,
            IReadOnlyDictionary<string, object> model,
            out string output,
            out string error)
        {
            output = null;
            error = null;

            if (string.IsNullOrEmpty(template))
            {
                error = "Template is empty.";
                return false;
            }

            if (!Parser.TryParse(template, out var parsed, out error))
                return false;

            var context = new TemplateContext(new TemplateOptions());
            foreach (var kv in model)
                context.SetValue(kv.Key, kv.Value);

            try
            {
                output = parsed.Render(context);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
