using Maintenance.Core.Models;
using RestSharp;
using Serilog;
using System;
using System.Configuration;

namespace Maintenance.Core.Externos
{
    public struct EVWEBAPI_ENDPOINTS
    {
        public const string EMAIL = "api/mail/send";
    }

    public class EvwebAPI
    {
        private readonly string _URL;

        public EvwebAPI()
        {
            _URL = ConfigurationManager.AppSettings["EvwebAPI"];
        }

        public void SendEmail(MailModel model)
        {
            try
            {
                RestClient client = new RestClient(_URL);
                RestRequest request = new RestRequest(EVWEBAPI_ENDPOINTS.EMAIL, Method.Post);
                request.AddBody(model);

                RestResponse response = client.Execute(request);

                if (!response.IsSuccessStatusCode) throw new Exception(response.Content);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ha ocurrido un error al intentar procesar la solicitud");
            }
        }
    }
}
