using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DLinkDspAPI.API
{
    /// <summary>
    /// HNAP socket client.
    /// </summary>
    internal class HnapClient : SoapClient
    {
        public static class MacAlgorithmNames
        {
            public static string HmacMd5 = "HmacMd5";
        }

        private const string HNAP1_XMLNS = "http://purenetworks.com/HNAP1/";
        private const string HNAP_LOGIN_METHOD = "Login";
        private const string HNAP_URI_FORMAT = "{0}://{1}{2}/HNAP1";
        private const int HNAP_DEFAULT_PORT = 80;

        private readonly string _hnapNamespace;

        private readonly HnapAuthenticationDescription _authentication = null;

        private bool _isHttpsEnabled = false;

        private int _port = HNAP_DEFAULT_PORT;

        private string _host;

        /// <summary>
        /// The host name.
        /// </summary>
        public string Host { get { return _host; } }

        private bool isReadOnly;

        /// <summary>
        /// Is the client read only?
        /// </summary>
        public bool IsReadOnly
        {
            get { return isReadOnly; }
            set { isReadOnly = value; }
        }

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="host">Host.</param>
        /// <param name="userName">User name.</param>
        /// <param name="password">Password.</param>
        /// <param name="port">Port.</param>
        /// <param name="isHttpsEnabled">Is HTTPS enabled?</param>
        /// <param name="isReadOnly">Is read only?</param>
        public HnapClient(string host, string userName, string password, int port = 80, bool isHttpsEnabled = false, bool isReadOnly = false)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentNullException("host");

            IsReadOnly = isReadOnly;
            _isHttpsEnabled = isHttpsEnabled;
            _port = port;
            _host = host;

            _authentication = new HnapAuthenticationDescription();
            _authentication.User = userName;
            _authentication.Password = password;
            _authentication.Uri = new Uri(string.Format(HNAP_URI_FORMAT, GetProtocol(_isHttpsEnabled), host, GetPort(_isHttpsEnabled)));

            // prepare namespaces in the syntax that is necessary for the XML parser
            _hnapNamespace = "{" + HNAP1_XMLNS + "}";
        }

        /// <summary>
        /// Get service URI.
        /// </summary>
        /// <returns>Service URI.</returns>
        protected override Uri GetServiceUri()
        {
            return _authentication.Uri;
        }

        /// <summary>
        /// Append request message headers.
        /// </summary>
        /// <param name="method">Requested method.</param>
        /// <param name="request">Request.</param>
        protected override void AppendRequestHeaders(string method, HttpRequestMessage request)
        {
            request.Headers.Add("SOAPAction", string.Format("\"{0}{1}\"", HNAP1_XMLNS, method));
            request.Headers.Add("HNAP_AUTH", GetHnapAuth(string.Format("\"{0}{1}\"", HNAP1_XMLNS, method), _authentication.PrivateKey));
            request.Headers.Add("Cookie", string.Format("uid={0}", _authentication.Cookie));
        }

        /// <summary>
        /// Login.
        /// </summary>
        /// <param name="user">User name.</param>
        /// <param name="password">Password.</param>
        /// <param name="uri">URI of the device.</param>
        /// <returns><see cref="true"/> if successful, <see cref="false"/> otherwise.</returns>
        public async Task<bool> LoginAsync()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _authentication.Uri);
            string body = GetRequestBody(HNAP_LOGIN_METHOD, HNAP1_XMLNS, GetLoginRequest(_authentication.User));
            request.Content = new StringContent(body);
            request.Content.Headers.Clear();
            request.Content.Headers.Add("Content-Type", "text/xml; charset=UTF-8");
            request.Headers.Add("SOAPAction", string.Format("\"{0}{1}\"", HNAP1_XMLNS, HNAP_LOGIN_METHOD));
            HttpResponseMessage response = null;

            try
            {
                response = await _client.SendAsync(request);
            }
            catch (Exception)
            {
                // no Internet connection most probably
                return false;
            }

            /*
             <soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <LoginResponse xmlns="http://purenetworks.com/HNAP1/">
                  <LoginResult>OK</LoginResult>
                  <Challenge>xxx</Challenge>
                  <Cookie>xxx</Cookie>
                  <PublicKey>xxx</PublicKey>
                </LoginResponse>
              </soap:Body>
            </soap:Envelope>
             */

            // parse the message
            XDocument xmlResponse = XDocument.Parse(await response.Content.ReadAsStringAsync());
            XElement xmlEnvelope = xmlResponse.Element(XName.Get(_soapNamespace + "Envelope"));
            if (xmlEnvelope != null)
            {
                XElement xmlBody = xmlEnvelope.Element(XName.Get(_soapNamespace + "Body"));
                XElement xmlLoginResponse = xmlBody.Element(XName.Get(_hnapNamespace + "LoginResponse"));

                _authentication.Result = xmlLoginResponse.Elements(XName.Get(_hnapNamespace + HNAP_LOGIN_METHOD + "Result")).First().Value;
                _authentication.Challenge = xmlLoginResponse.Elements(XName.Get(_hnapNamespace + "Challenge")).First().Value;
                _authentication.PublicKey = xmlLoginResponse.Elements(XName.Get(_hnapNamespace + "PublicKey")).First().Value;
                _authentication.Cookie = xmlLoginResponse.Elements(XName.Get(_hnapNamespace + "Cookie")).First().Value;
                _authentication.PrivateKey = HashWith(_authentication.Challenge, MacAlgorithmNames.HmacMd5, _authentication.PublicKey + _authentication.Password).ToUpperInvariant();

                string responseValue = await base.SoapActionAsync(HNAP_LOGIN_METHOD, _hnapNamespace + "LoginResult", GetRequestBody(HNAP_LOGIN_METHOD, HNAP1_XMLNS, GetLoginParameters()));
                return string.Compare("success", responseValue) == 0;
            }
            else
            {
                return false;
            }
        }

        private string GetLoginParameters()
        {
            string loginPwd = HashWith(_authentication.Challenge, MacAlgorithmNames.HmacMd5, _authentication.PrivateKey);
            return string.Format(
                "<Action>login</Action>" +
                "<Username>{0}</Username>" +
                "<LoginPassword>{1}</LoginPassword>" +
                "<Captcha></Captcha>",
                _authentication.User,
                loginPwd.ToUpperInvariant());
        }

        private string GetLoginRequest(string user)
        {
            return string.Format(
                "<Action>request</Action>" +
                "<Username>{0}</Username>" +
                "<LoginPassword></LoginPassword>" +
                "<Captcha></Captcha>",
                user);
        }

        private string GetHnapAuth(string soapAction, string privateKey)
        {
            var currentTime = DateTime.Now;
            var timeStamp = Math.Ceiling(currentTime.Ticks / 10000000d);
            var auth = HashWith(timeStamp + soapAction, MacAlgorithmNames.HmacMd5, privateKey);
            return auth.ToUpperInvariant() + " " + timeStamp;
        }

        protected override async Task<string> SoapActionAsync(string method, string responseElement, string body)
        {
            // try to re-login if the action fails
            string ret = await base.SoapActionAsync(method, responseElement, body);
            if ((string.IsNullOrEmpty(ret) || "ERROR".CompareTo(ret) == 0) && _authentication != null)
            {
                await LoginAsync();
                ret = await base.SoapActionAsync(method, responseElement, body);
            }
            return ret;
        }

        private static string HashWith(string input, string hashName, string key)
        {
            if (hashName.CompareTo(MacAlgorithmNames.HmacMd5) == 0)
            {
                HMACMD5 md5Hasher = new HMACMD5(Encoding.UTF8.GetBytes(key));
                return BitConverter.ToString(md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", string.Empty);
            }
            else
            {
                throw new NotSupportedException(hashName);
            }
        }

        private static string AES_Encrypt128(string input, string key)
        {
            var aesAlg = new AesManaged
            {
                KeySize = 128,
                Key = Encoding.UTF8.GetBytes(key),
                BlockSize = 128,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.Zeros,
                IV = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
            };

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            byte[] encrypted = encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(input), 0, input.Length);
            return BitConverter.ToString(encrypted);
        }
        private string GetModuleParameters(int module)
        {
            return string.Format("<ModuleID>{0}</ModuleID>", module);
        }

        private string GetControlParameters(int module, bool status, string nickName = "Socket 1", string description = "Socket 1", int controller = 1)
        {
            return string.Format(
                "{0}" +
                "<NickName>{1}</NickName>" +
                "<Description>{2}</Description>" +
                "<OPStatus>{3}</OPStatus>" +
                "<Controller>{4}</Controller>",
                GetModuleParameters(module),
                nickName,
                description,
                status,
                controller);
        }

        private string GetRadioParameters(string radio)
        {
            return string.Format("<RadioID>{0}</RadioID>", radio);
        }

        private string GetAPClientParameters(string SSID = "My_Network", string macAddress = "XX:XX:XX:XX:XX:XX", string password = "password", bool isEnabled = true, string radioID = "RADIO_2.4GHz", int channelWidth = 0)
        {
            return string.Format(
                "<Enabled>{0}</Enabled>" +
                "<RadioID>{1}</RadioID>" +
                "<SSID>{2}</SSID>" +
                "<MacAddress>{3}</MacAddress>" +
                "<ChannelWidth>{4}</ChannelWidth>" +
                "<SupportedSecurity>" +
                "<SecurityInfo>" +
                "<SecurityType>WPA2-PSK</SecurityType>" +
                "<Encryptions>" +
                "<string>AES</string>" +
                "</Encryptions>" +
                "</SecurityInfo>" +
                "</SupportedSecurity>" +
                "<Key>{5}</Key>",
                isEnabled,
                radioID,
                SSID,
                macAddress,
                channelWidth,
                AES_Encrypt128(password, _authentication.PrivateKey));
        }

        private string GetGroupParameters(int group)
        {
            return string.Format("<ModuleGroupID>{0}</ModuleGroupID>", group);
        }

        private string GetTemperatureSettingsParameters(int module, string nickName = "TemperatureMonitor 3", string description = "Temperature Monitor 3", string upperBound = "80", string lowerBound = "Not Available", bool opsStatus = true)
        {
            return string.Format(
                "{0}" +
                "<NickName>{1}</NickName>" +
                "<Description>{2}</Description>" +
                "<UpperBound>{3}</UpperBound>" +
                "<LowerBound>{4}</LowerBound>" +
                "<OPStatus>{5}</OPStatus>",
                GetModuleParameters(module),
                nickName,
                description,
                upperBound,
                lowerBound,
                opsStatus);
        }

        private string GetPowerWarningParameters(int threshold = 28, int percentage = 70, string periodicType = "Weekly", int startTime = 1)
        {
            return string.Format(
                "<Threshold>{0}</Threshold>" +
                "<Percentage>{1}</Percentage>" +
                "<PeriodicType>{2}</PeriodicType>" +
                "<StartTime>{3}</StartTime>",
                threshold,
                percentage,
                periodicType,
                startTime);
        }

        /// <summary>
        /// Turn the socket on.
        /// </summary>
        /// <returns>OK if successful.</returns>
        public async Task<bool> TurnOnAsync()
        {
            if (!IsReadOnly)
            {
                string result = await SoapActionAsync("SetSocketSettings", _hnapNamespace + "SetSocketSettingsResult", GetRequestBody("SetSocketSettings", HNAP1_XMLNS, GetControlParameters(1, true)));

                if (!string.IsNullOrEmpty(result))
                    return "OK".CompareTo(result) == 0;
                else
                    return false;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> TurnOffAsync()
        {
            if (!IsReadOnly)
            {
                string result = await SoapActionAsync("SetSocketSettings", _hnapNamespace + "SetSocketSettingsResult", GetRequestBody("SetSocketSettings", HNAP1_XMLNS, GetControlParameters(1, false)));
                if (!string.IsNullOrEmpty(result))
                    return "OK".CompareTo(result) == 0;
                else
                    return false;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Get the current state of the socket.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> GetStateAsync()
        {
            string response = await SoapActionAsync("GetSocketSettings", _hnapNamespace + "OPStatus", GetRequestBody("GetSocketSettings", HNAP1_XMLNS, GetModuleParameters(1)));
            if (!string.IsNullOrEmpty(response))
                return "true".CompareTo(response.ToLowerInvariant()) == 0;
            else
                return false;
        }

        /// <summary>
        /// Retreive current power consumption from the socket.
        /// </summary>
        /// <returns>Current power consumtion.</returns>
        public async Task<double> GetPowerConsumptionAsync()
        {
            double ret = double.NaN;
            string response = await SoapActionAsync("GetCurrentPowerConsumption", _hnapNamespace + "CurrentConsumption", GetRequestBody("GetCurrentPowerConsumption", HNAP1_XMLNS, GetModuleParameters(2)));

            if (!string.IsNullOrEmpty(response))
                double.TryParse(response, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out ret);

            return ret;
        }

        /// <summary>
        /// Retreive total power consumption from the socket.
        /// </summary>
        /// <returns>Total power consumtion.</returns>
        public async Task<double> GetTotalPowerConsumptionAsync()
        {
            double ret = double.NaN;
            string response = await SoapActionAsync("GetPMWarningThreshold", _hnapNamespace + "TotalConsumption", GetRequestBody("GetPMWarningThreshold", HNAP1_XMLNS, GetModuleParameters(2)));

            if (!string.IsNullOrEmpty(response))
                double.TryParse(response, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out ret);

            return ret;
        }

        /// <summary>
        /// Retrieve current temperature from the socket.
        /// </summary>
        /// <returns>Current temperature.</returns>
        public async Task<double> GetTemperatureAsync()
        {
            double ret = double.NaN;
            string response = await SoapActionAsync("GetCurrentTemperature", _hnapNamespace + "CurrentTemperature", GetRequestBody("GetCurrentTemperature", HNAP1_XMLNS, GetModuleParameters(3)));

            if (!string.IsNullOrEmpty(response))
                double.TryParse(response, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out ret);

            return ret;
        }

        public Task<string> GetAPClientSettingsAsync()
        {
            return SoapActionAsync("GetAPClientSettings", _hnapNamespace + "GetAPClientSettingsResult", GetRequestBody("GetAPClientSettings", HNAP1_XMLNS, GetRadioParameters("RADIO_2.4GHz")));
        }

        public Task<string> SetPowerWarningAsync()
        {
            return SoapActionAsync("SetPMWarningThreshold", _hnapNamespace + "SetPMWarningThresholdResult", GetRequestBody("SetPMWarningThreshold", HNAP1_XMLNS, GetPowerWarningParameters()));
        }

        public Task<string> GetPowerWarningAsync()
        {
            return SoapActionAsync("GetPMWarningThreshold", _hnapNamespace + "GetPMWarningThresholdResult", GetRequestBody("GetPMWarningThreshold", HNAP1_XMLNS, GetModuleParameters(2)));
        }

        public Task<string> GetTemperatureSettingsAsync()
        {
            return SoapActionAsync("GetTempMonitorSettings", _hnapNamespace + "GetTempMonitorSettingsResult", GetRequestBody("GetTempMonitorSettings", HNAP1_XMLNS, GetModuleParameters(3)));
        }

        public Task<string> SetTemperatureSettingsAsync()
        {
            return SoapActionAsync("SetTempMonitorSettings", _hnapNamespace + "SetTempMonitorSettingsResult", GetRequestBody("SetTempMonitorSettings", HNAP1_XMLNS, GetTemperatureSettingsParameters(3)));
        }

        public Task<string> GetSiteSurveyAsync()
        {
            return SoapActionAsync("GetSiteSurvey", _hnapNamespace + "GetSiteSurveyResult", GetRequestBody("GetSiteSurvey", HNAP1_XMLNS, GetRadioParameters("RADIO_2.4GHz")));
        }

        public Task<string> TriggerWirelessSiteSurveyAsync()
        {
            return SoapActionAsync("SetTriggerWirelessSiteSurvey", _hnapNamespace + "SetTriggerWirelessSiteSurveyResult", GetRequestBody("SetTriggerWirelessSiteSurvey", HNAP1_XMLNS, GetRadioParameters("RADIO_2.4GHz")));
        }

        public Task<string> LatestDetectionAsync()
        {
            return SoapActionAsync("GetLatestDetection", _hnapNamespace + "GetLatestDetectionResult", GetRequestBody("GetLatestDetection", HNAP1_XMLNS, GetModuleParameters(2)));
        }

        public Task<string> RebootAsync()
        {
            return SoapActionAsync("Reboot", _hnapNamespace + "RebootResult", GetRequestBody("Reboot", HNAP1_XMLNS, ""));
        }

        public Task<string> IsDeviceReadyAsync()
        {
            return SoapActionAsync("IsDeviceReady", _hnapNamespace + "IsDeviceReadyResult", GetRequestBody("IsDeviceReady", HNAP1_XMLNS, ""));
        }

        public Task<string> GetModuleScheduleAsync()
        {
            return SoapActionAsync("GetModuleSchedule", _hnapNamespace + "GetModuleScheduleResult", GetRequestBody("GetModuleSchedule", HNAP1_XMLNS, GetModuleParameters(0)));
        }

        public Task<string> GetModuleEnabledAsync()
        {
            return SoapActionAsync("GetModuleEnabled", _hnapNamespace + "GetModuleEnabledResult", GetRequestBody("GetModuleEnabled", HNAP1_XMLNS, GetModuleParameters(0)));
        }

        public Task<string> GetModuleGroupAsync()
        {
            return SoapActionAsync("GetModuleGroup", _hnapNamespace + "GetModuleGroupResult", GetRequestBody("GetModuleGroup", HNAP1_XMLNS, GetGroupParameters(0)));
        }

        public Task<string> GetScheduleSettingsAsync()
        {
            return SoapActionAsync("GetScheduleSettings", _hnapNamespace + "GetScheduleSettingsResult", GetRequestBody("GetScheduleSettings", HNAP1_XMLNS, ""));
        }

        public Task<string> SetFactoryDefaultAsync()
        {
            return SoapActionAsync("SetFactoryDefault", _hnapNamespace + "SetFactoryDefaultResult", GetRequestBody("SetFactoryDefault", HNAP1_XMLNS, ""));
        }

        public Task<string> GetWLanRadiosAsync()
        {
            return SoapActionAsync("GetWLanRadios", _hnapNamespace + "GetWLanRadiosResult", GetRequestBody("GetWLanRadios", HNAP1_XMLNS, ""));
        }

        public Task<string> GetInternetSettingsAsync()
        {
            return SoapActionAsync("GetInternetSettings", _hnapNamespace + "GetInternetSettingsResult", GetRequestBody("GetInternetSettings", HNAP1_XMLNS, ""));
        }

        public Task<string> SetAPClientSettingsAsync()
        {
            return SoapActionAsync("SetAPClientSettings", _hnapNamespace + "SetAPClientSettingsResult", GetRequestBody("SetAPClientSettings", HNAP1_XMLNS, GetAPClientParameters()));
        }

        public Task<string> SetTriggerADICAsync()
        {
            return SoapActionAsync("SettriggerADIC", _hnapNamespace + "SettriggerADICResult", GetRequestBody("SettriggerADIC", HNAP1_XMLNS, ""));
        }

        private static string GetProtocol(bool isHttpsEnabled)
        {
            return isHttpsEnabled ? "https" : "http";
        }

        private string GetPort(bool isHttpsEnabled)
        {
            return isHttpsEnabled ? string.Format(":{0}", _port) : string.Empty;
        }
    }
}
