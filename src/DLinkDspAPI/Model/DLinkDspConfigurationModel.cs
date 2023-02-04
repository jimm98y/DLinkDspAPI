namespace DLinkDspAPI.Model
{
    public class DLinkDspConfigurationModel
    {
        public string Type { get { return nameof(DLinkDspConfigurationModel); } }

        public string Name { get; set; }

        public string Host { get; set; }

        public double Consumption { get; set; }

        public double TotalConsumption { get; set; }

        public double Temperature { get; set; }

        public bool IsOn { get; set; }

        public bool IsReadOnly { get; set; }
    }
}
