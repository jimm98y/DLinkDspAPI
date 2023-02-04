using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DLinkDspAPI.API
{
    /// <summary>
    /// Soap client.
    /// </summary>
    internal abstract class SoapClient : IDisposable
    {
        protected const string SOAP_XMLNS = "http://schemas.xmlsoap.org/soap/envelope/";

        protected readonly string _soapNamespace;
        protected HttpClient _client;

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="client">HTTP client.</param>
        public SoapClient()
        {
            _client = new HttpClient(new HttpClientHandler() { UseCookies = false }); // disable cookies in order to be able to add Cookie header manually

            // prepare namespaces in the syntax that is necessary for the XML parser
            _soapNamespace = "{" + SOAP_XMLNS + "}";
        }

        protected abstract Uri GetServiceUri();

        protected virtual async Task<string> SoapActionAsync(string method, string responseElement, string body)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, GetServiceUri());
            request.Content = new StringContent(body);

            request.Content.Headers.Clear(); // HttpStringContent already fills in text content header, call Clear to clear all predefined headers and supply our own
            request.Content.Headers.Add("Content-Type", "text/xml; charset=utf-8");
            AppendRequestHeaders(method, request);

            try
            {
                HttpResponseMessage response = await _client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                    return await ReadResponseValueAsync(response, responseElement);
                else
                    return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        protected virtual void AppendRequestHeaders(string method, HttpRequestMessage request)
        { }

        protected async Task<string> ReadResponseValueAsync(HttpResponseMessage response, string responseElement)
        {
            try
            {
                // parse the message
                XDocument xmlResponse = XDocument.Parse(await response.Content.ReadAsStringAsync());
                XElement xmlEnvelope = xmlResponse.Element(XName.Get(_soapNamespace + "Envelope"));
                XElement xmlBody = xmlEnvelope.Element(XName.Get(_soapNamespace + "Body"));
                XElement xmlSoapResponse = xmlBody.Elements().First();

                // use Descendants here because it will search for the name in the whole subtree, not only on the current level
                return xmlSoapResponse.Descendants(XName.Get(responseElement)).First().Value;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        protected string GetRequestBody(string method, string ns, string parameters)
        {
            return string.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<soap:Envelope " +
                    "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
                    "xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" " +
                    "xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                    "<soap:Body>" +
                        "<{0} xmlns=\"{1}\">" +
                            "{2}" +
                        "</{3}>" +
                    "</soap:Body>" +
                "</soap:Envelope>",
                method,
                ns,
                parameters,
                method);
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _client.Dispose();
                    _client = null;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion // IDisposable Support
    }
}
