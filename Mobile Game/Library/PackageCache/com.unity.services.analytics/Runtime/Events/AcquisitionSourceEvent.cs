using UnityEngine;

namespace Unity.Services.Analytics
{
    /// <summary>
    /// Use this class to record acquisitionSource events.
    ///
    /// For more information about the acquisitionSource event, see the documentation page:
    /// https://docs.unity.com/ugs/en-us/manual/analytics/manual/attribution-support
    /// </summary>
    public class AcquisitionSourceEvent : Event
    {
        public AcquisitionSourceEvent() : base("acquisitionSource", true, 1)
        {
        }

        /// <summary>
        /// (Required) The name of the specific marketing provider used to drive traffic to the game.
        /// This should be a short identifiable string as this will be the name displayed when filtering or grouping by an acquisition channel.
        /// </summary>
        public string AcquisitionChannel { set { SetParameter("acquisitionChannel", value); } }

        /// <summary>
        /// (Required) The ID of the acquisition campaign.
        /// </summary>
        public string AcquisitionCampaignId { set { SetParameter("acquisitionCampaignId", value); } }

        /// <summary>
        /// (Required) The ID of the acquisition campaign creative.
        /// </summary>
        public string AcquisitionCreativeId { set { SetParameter("acquisitionCreativeId", value); } }

        /// <summary>
        /// (Required) The name of the acquisition campaign e.g. Interstitial:Halloween21.
        /// </summary>
        public string AcquisitionCampaignName { set { SetParameter("acquisitionCampaignName", value); } }

        /// <summary>
        /// (Required) The name of the attribution provider in use e.g. Adjust, AppsFlyer, Singular
        /// </summary>
        public string AcquisitionProvider { set { SetParameter("acquisitionProvider", value); } }

        /// <summary>
        /// (Optional) The cost of the install e.g. 2.36.
        /// </summary>
        public float AcquisitionCost { set { SetParameter("acquisitionCost", value); } }

        /// <summary>
        /// (Optional) The ISO 4217 three-letter currency code for the install cost currency. For example, GBP or USD.
        /// </summary>
        public string AcquisitionCostCurrency { set { SetParameter("acquisitionCostCurrency", value); } }

        /// <summary>
        /// (Optional) The acquisition campaign network e.g. Ironsource, Facebook Ads.
        /// </summary>
        public string AcquisitionNetwork { set { SetParameter("acquisitionNetwork", value); } }

        /// <summary>
        /// (Optional) The acquisition campaign type. e.g. CPI.
        /// </summary>
        public string AcquisitionCampaignType { set { SetParameter("acquisitionCampaignType", value); } }

        public override void Validate()
        {
            base.Validate();

            if (!ParameterHasBeenSet("acquisitionChannel"))
            {
                Debug.LogWarning("A value for the AcquisitionChannel parameter is required for an AcquisitionSource event.");
            }

            if (!ParameterHasBeenSet("acquisitionCampaignId"))
            {
                Debug.LogWarning("A value for the AcquisitionCampaignId parameter is required for an AcquisitionSource event.");
            }

            if (!ParameterHasBeenSet("acquisitionCreativeId"))
            {
                Debug.LogWarning("A value for the AcquisitionCreativeId parameter is required for an AcquisitionSource event.");
            }

            if (!ParameterHasBeenSet("acquisitionCampaignName"))
            {
                Debug.LogWarning("A value for the AcquisitionCampaignName parameter is required for an AcquisitionSource event.");
            }

            if (!ParameterHasBeenSet("acquisitionProvider"))
            {
                Debug.LogWarning("A value for the AcquisitionProvider parameter is required for an AcquisitionSource event.");
            }
        }
    }
}
