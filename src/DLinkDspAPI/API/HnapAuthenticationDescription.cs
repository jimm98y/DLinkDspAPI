using System;

namespace DLinkDspAPI.API
{
    /// <summary>
    /// HNAP authentication description.
    /// </summary>
    internal class HnapAuthenticationDescription
    {
        /// <summary>
        /// User name. "admin" by default.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// Password. PIN of the device.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// URI of the HNAP device. http://IP_ADDRESS/HNAP1 by default.
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// Authentication result.
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// Authentication challenge.
        /// </summary>
        public string Challenge { get; set; }

        /// <summary>
        /// Public key.
        /// </summary>
        public string PublicKey { get; set; }

        /// <summary>
        /// Cookie.
        /// </summary>
        public string Cookie { get; set; }

        /// <summary>
        /// Private key.
        /// </summary>
        public string PrivateKey { get; set; }
    }
}
