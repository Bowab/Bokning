using Easyweb.Core;
using Easyweb.Core.Attributes;
using Easyweb.Core.Extensions;
using Easyweb.Core.Templates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Albatross.Infrastructure
{
    [RendersFor(nameof(ReferenceType), nameof(ReferenceType.PdfGenerator))]
    public class PdfGeneratorComponent : AutoListRenderable<LinkReferenceTypeOptions>, ILinkableComponent, IActionResultComponentAsync
    {
        public override string TagName
        {
            get => !String.IsNullOrEmpty(this.Options.TagName)
                ? this.Options.TagName
                : base.TagName;
            set => base.TagName = value;
        }
        public ILinkable Linkable { get; protected set; }
        public override string Label => !String.IsNullOrEmpty(WebContent?.Label) ? WebContent.Label : Linkable?.Label ?? "";
        public string ObjectLabel => Linkable?.Label ?? "";

        private string EasywebUrl => RenderContext.SiteOptions.PdfGeneratorUrl;

        protected override IRenderable Contextualize()
        {
            if (IsContextualized) return this;

            if (WebContent != null && WebContent.RefObjectId != null && WebContent.RefWebModuleId != null)
                Linkable = RenderContext.DataSource.FindLinkable(WebContent);

            var templateUrl = "";
            var key = !String.IsNullOrEmpty(this.CustomKey) ? this.CustomKey : "PdfGenerator";
            var path = ClientContext.Context.Request.Path;

            if (path == "/")
                path = "/home";

            if (this.Options.SelfUrl ?? false)
            {
                templateUrl = String.Format("/template{0}?key={1}&dataurl={2}", path.Value, key, RenderContext.UrlHelper.Action(this.FindClosestArticle()));
                Linkable = null;
                Value = templateUrl;
            }
            else
            {
                if (Linkable != null)
                {
                    templateUrl = String.Format("/template{0}?key={1}&dataurl={2}", path.Value, key, RenderContext.UrlHelper.Action(Linkable));
                    Linkable = null;
                    Value = templateUrl;
                }
            }

            base.Contextualize();
            IsContextualized = true;
            return this;
        }


        public override Task<bool> DefaultHtmlContent(ViewContext viewContext)
        {
            if (IsList)
                return base.DefaultHtmlListContent(viewContext);

            if (String.IsNullOrEmpty(Value) && String.IsNullOrEmpty(WebContent?.Label))
                return Task.FromResult(false);

            var templateUrl = Value;

            // Create tag and set href and inner content as label
            //            
            var tag = new TagBuilder("a");
            tag.MergeAttribute("href", templateUrl);
            tag.InnerHtml.Append(WebContent?.Label);
            tag.Attributes.Add("target", "_blank");

            // Set possible css-classes
            //
            if (!Parent.IsList && !String.IsNullOrEmpty(Options.CssClasses))
                tag.AddCssClass(Options.CssClasses);
            if (Parent.IsList && !String.IsNullOrEmpty(Options.ItemCssClasses))
                tag.AddCssClass(Options.ItemCssClasses);

            // Set possible target from webcontent options
            //
            var target = WebContent?.Options?.CustomValues?.ContainsKey("target") ?? false
                ? WebContent?.Options?.CustomValues["target"]
                : null;

            if (!String.IsNullOrEmpty(target))
                tag.MergeAttribute("target", target);

            return Render(viewContext, tag);
        }


        /// <summary>
        /// The action result for calls to /api/[route]/...?key=[templateKey] or /api/[key]/[route(url)]
        /// Json ex: return new JsonResult(myitem);
        /// </summary>
        public Task<IActionResult> ApiResultAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The action result for calls to /template/[route]/...?key=[templateKey]
        /// </summary>
        public async Task<IActionResult> TemplateResultAsync()
        {
            var clientFactory = ClientContext.Context.RequestServices.GetService(typeof(IHttpClientFactory)) as IHttpClientFactory;

            var dataUrl = ClientContext.Context.Request.Query["dataUrl"];

            string pdfUrl;
            if (ClientContext.Context.Request.Host.Host == "localhost")
            {
                pdfUrl = ClientContext.Context.Request.Scheme + "://" + RenderContext.DataSource.Union.Url + dataUrl;
            }
            else
            {
                pdfUrl = ClientContext.Context.Request.Scheme + "://" + ClientContext.Context.Request.Host.Value + dataUrl;

            }

            var completePdfUrl = String.Format(EasywebUrl + "?url={0}&type=pdf", pdfUrl);

            using (var client = clientFactory.CreateClient())
            {
                var response = await client.GetStreamAsync(completePdfUrl);
                return new FileStreamResult(response, "application/pdf");
            }

        }
    }
}
