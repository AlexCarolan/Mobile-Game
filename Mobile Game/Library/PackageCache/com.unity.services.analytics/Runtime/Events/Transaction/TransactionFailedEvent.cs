using System.Collections.Generic;
using Unity.Services.Analytics.Internal;
using UnityEngine;

namespace Unity.Services.Analytics
{
    /// <summary>
    /// Use this class to record transactionFailed events.
    ///
    /// For more information about the transactionFailed event, see the documentation page:
    /// https://docs.unity.com/ugs/en-us/manual/analytics/manual/record-transaction-events
    /// </summary>
    public class TransactionFailedEvent : TransactionEvent
    {
        public TransactionFailedEvent() : base("transactionFailed")
        {
        }

        /// <summary>
        /// (Required) The reason why this transaction failed.
        /// </summary>
        public string FailureReason { set { SetParameter("failureReason", value); } }

        public override void Validate()
        {
            base.Validate();

            if (!ParameterHasBeenSet("failureReason"))
            {
                Debug.LogWarning("A value for the FailureReason parameter is required for a TransactionFailed event.");
            }
        }
    }
}
