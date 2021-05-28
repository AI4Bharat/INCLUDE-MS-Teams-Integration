using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AI4Bharat.ISLBot.Services.Psi
{
    public class CallModelComponent : AsyncConsumerProducer<string, (string filename, string label)>, IDisposable
    {
        private HttpClient client;
        private string endpointUrl;
        private readonly string basePath;
        private readonly IGraphLogger logger;

        public CallModelComponent(Pipeline pipeline, string endpointUrl, string basePath, IGraphLogger logger) : base(pipeline)
        {
            this.client = new HttpClient();
            this.endpointUrl = endpointUrl;
            this.basePath = basePath;
            this.logger = logger;
        }

        protected override async Task ReceiveAsync(string f, Envelope e)
        {
            try
            {
                // Fire off the request query asynchronously.
                //f = "local.MOV";
                
                var response = await this.client.PostAsync(
                    $"{endpointUrl}?from_local=True&local_file_path={basePath}&file_name={Path.GetFileName(f)}", null);

                response.EnsureSuccessStatusCode();
                // Read the HTML into a string and start scraping.
                string json = await response.Content.ReadAsStringAsync();
                // Deserialize the JSON into an IntentData object.
                var modelResponse = JsonConvert.DeserializeObject<ModelResponse>(json);
                this.Out.Post((f, modelResponse.predicted_label), e.OriginatingTime);
            }
            catch (HttpRequestException ex)
            {
                logger.Error(ex, $"Error while sending request to model. Message: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Unknown Error while sending request to model. Message: {ex.Message}");
            }
        }

        public void Dispose()
        {
            this.client.Dispose();
        }

        private class ModelResponse
        {
            public string predicted_label { get; set; }
        }
    }

}
