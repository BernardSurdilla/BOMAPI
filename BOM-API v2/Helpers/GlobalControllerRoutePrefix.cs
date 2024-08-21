using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace BOM_API_v2.Helpers
{
    public class GlobalControllerRoutePrefix : RouteAttribute, IRouteTemplateProvider
    {
        public GlobalControllerRoutePrefix(string template) : base(template)
        {
        }
    }

    public class GlobalControllerRoutePrefixConvention : IApplicationModelConvention
    {
        private readonly AttributeRouteModel _centralPrefix;

        public GlobalControllerRoutePrefixConvention(IRouteTemplateProvider routeTemplateProvider)
        {
            _centralPrefix = new AttributeRouteModel(routeTemplateProvider);
        }

        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                foreach (var selector in controller.Selectors)
                {
                    if (selector.AttributeRouteModel != null)
                    {
                        selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(_centralPrefix, selector.AttributeRouteModel);
                    }
                    else
                    {
                        selector.AttributeRouteModel = _centralPrefix;
                    }
                }
            }
        }
    }

}
