using DLinkDspAPI.API;
using DLinkDspAPI.Model;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DLinkDspAPI
{
    /// <summary>
    /// DLink DSP Client.
    /// </summary>
    public class DLinkDspClient : IDisposable
    {
        private const int HNAP_REFRESH_INTERVAL = 10000; // 10 seconds interval between updates

        private readonly DLinkDspConfigurationModel _configuration = new DLinkDspConfigurationModel();
        private readonly HnapClient _client;
        private DateTime _lastRefreshTimeStamp = DateTime.UtcNow;

        /// <summary>
        /// The host name.
        /// </summary>
        public string Host
        {
            get { return _client.Host; }
        }

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="name">Optional name of the device.</param>
        /// <param name="host">IP Address of the smart socket.</param>
        /// <param name="isHttpsEnabled">Should we use HTTPS to communicate with the socket?</param>
        /// <param name="port">Port. Default is 80.</param>
        /// <param name="isReadOnly">Should we just read data and not write anything?</param>
        public DLinkDspClient(string name, string host, string userName, string password, int port = 80, bool isHttpsEnabled = false, bool isReadOnly = false)
        {
            this._client = new HnapClient(host, userName, password, port, isHttpsEnabled, isReadOnly);
            this._configuration.Name = name;
            this._configuration.Host = _client.Host;
            this._configuration.IsReadOnly = this._client.IsReadOnly;
        }

        public async Task InitializeAsync()
        {
            await _client.LoginAsync();
            await RefreshAsync();
        }

        private async void Refresh()
        {
            try
            {
                await RefreshAsync();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private async Task<DLinkDspConfigurationModel> RefreshAsync()
        {
            double consumption = await _client.GetPowerConsumptionAsync();
            double totalPowerConsumption = await _client.GetTotalPowerConsumptionAsync();
            double temperature = await _client.GetTemperatureAsync();
            bool state = await _client.GetStateAsync();

            _configuration.Consumption = consumption;
            _configuration.TotalConsumption = totalPowerConsumption;
            _configuration.Temperature = temperature;
            _configuration.IsOn = state;

            _lastRefreshTimeStamp = DateTime.UtcNow;

            return _configuration;
        }

        public DLinkDspConfigurationModel GetConfiguration(int refreshInterval = HNAP_REFRESH_INTERVAL)
        {
            // if the current configuration is older than 10 seconds, trigger refresh
            if (DateTime.UtcNow.Subtract(_lastRefreshTimeStamp).TotalMilliseconds > refreshInterval)
            {
                Refresh();
            }

            return _configuration;
        }

        public async Task<bool> ExecuteActionAsync(string action)
        {
            bool ret = false;
            if (HnapActions.ACTION_ON.CompareTo(action) == 0)
            {
                await TurnOnAsync();
                ret = true;
            }
            else if (HnapActions.ACTION_OFF.CompareTo(action) == 0)
            {
                await TurnOffAsync();
                ret = true;
            }
            else if (HnapActions.ACTION_TOGGLE.CompareTo(action) == 0)
            {
                if (_configuration.IsOn)
                {
                    await TurnOffAsync();
                }
                else
                {
                    await TurnOnAsync();
                }

                ret = true;
            }

            return ret;
        }

        public async Task TurnOnAsync()
        {
            if (await _client.TurnOnAsync())
            {
                _configuration.IsOn = true;
            }
        }

        public async Task TurnOffAsync()
        {
            if (await _client.TurnOffAsync())
            {
                _configuration.IsOn = false;
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this._client.Dispose();
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
