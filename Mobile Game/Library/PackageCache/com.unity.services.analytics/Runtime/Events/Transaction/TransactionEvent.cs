using System;
using System.Collections.Generic;
using Unity.Services.Analytics.Internal;
using UnityEngine;

namespace Unity.Services.Analytics
{
    public class TransactionRealCurrency
    {
        /// <summary>
        /// (Required) The ISO 4217 three-letter currency code for the real currency. For example, GBP or USD.
        /// </summary>
        public string RealCurrencyType;

        /// <summary>
        /// (Required) The amount of real currency, in the smallest unit applicable to that currency.
        /// The <c>AnalyticsService.Instance.ConvertCurrencyToMinorUnits</c> method is available to convert a decimal currency value into minor units if required.
        /// </summary>
        public long RealCurrencyAmount;

        internal void Serialize(IBuffer buffer)
        {
            buffer.PushString("realCurrencyType", RealCurrencyType);
            buffer.PushInt64("realCurrencyAmount", RealCurrencyAmount);
        }
    }

    public class TransactionItem
    {
        /// <summary>
        /// (Required) The name of the item.
        /// </summary>
        public string ItemName;

        /// <summary>
        /// (Required) The type of the item, e.g. "weapon" or "spell".
        /// </summary>
        public string ItemType;

        /// <summary>
        /// (Required) The amount of the item.
        /// </summary>
        public long ItemAmount;

        internal void Serialize(IBuffer buffer)
        {
            buffer.PushString("itemName", ItemName);
            buffer.PushString("itemType", ItemType);
            buffer.PushInt64("itemAmount", ItemAmount);
        }
    }

    public class TransactionVirtualCurrency
    {
        private readonly static string[] k_VirtualCurrencyTypeValues = Event.BakeEnum2String<VirtualCurrencyType>();

        /// <summary>
        /// (Required) The name of the virtual currency.
        /// </summary>
        public string VirtualCurrencyName;

        /// <summary>
        /// (Required) The type of the virtual currency.
        /// </summary>
        public VirtualCurrencyType VirtualCurrencyType;

        /// <summary>
        /// (Required) The amount of the virtual currency.
        /// </summary>
        public long VirtualCurrencyAmount;

        internal void Serialize(IBuffer buffer)
        {
            buffer.PushString("virtualCurrencyName", VirtualCurrencyName);
            buffer.PushString("virtualCurrencyType", k_VirtualCurrencyTypeValues[(int)VirtualCurrencyType]);
            buffer.PushInt64("virtualCurrencyAmount", VirtualCurrencyAmount);
        }
    }

    /// <summary>
    /// Use this class to record transaction events.
    ///
    /// For more information about the transaction event, see the documentation page:
    /// https://docs.unity.com/ugs/en-us/manual/analytics/manual/record-transaction-events
    /// </summary>
    public class TransactionEvent : Event
    {
        private static readonly string[] k_TransactionTypeValues = Event.BakeEnum2String<TransactionType>();
        private static readonly string[] k_TransactionServerValues = Event.BakeEnum2String<TransactionServer>();

        public TransactionEvent() : this("transaction")
        {
        }

        protected internal TransactionEvent(string name) : base(name, true, 1)
        {
            SpentVirtualCurrencies = new List<TransactionVirtualCurrency>();
            SpentItems = new List<TransactionItem>();
            ReceivedVirtualCurrencies = new List<TransactionVirtualCurrency>();
            ReceivedItems = new List<TransactionItem>();
        }

        /// <summary>
        /// (Required) A name that describes the transaction, for example "BUY GEMS" or "BUY ITEMS".
        /// </summary>
        public string TransactionName { set { SetParameter("transactionName", value); } }

        /// <summary>
        /// (Optional) A unique identifier for this specific transaction.
        /// </summary>
        public string TransactionId { set { SetParameter("transactionID", value); } }

        /// <summary>
        /// (Required) The type of the transaction.
        /// </summary>
        public TransactionType TransactionType { set { SetParameter("transactionType", k_TransactionTypeValues[(int)value]); } }

        /// <summary>
        /// (Optional) The country where this transaction is taking place.
        /// </summary>
        public string PaymentCountry { set { SetParameter("paymentCountry", value); } }

        /// <summary>
        /// (Optional) The product identifier (known as a SKU) found in the store.
        /// </summary>
        public string ProductId { set { SetParameter("productID", value); } }

        /// <summary>
        /// (Optional) A unique identifier for the SKU, linked to the store SKU identifier.
        /// </summary>
        public string StoreItemSkuId { set { SetParameter("storeItemSkuID", value); } }

        /// <summary>
        /// (Optional) A unique identifier for the purchased item.
        /// </summary>
        public string StoreItemId { set { SetParameter("storeItemID", value); } }

        /// <summary>
        /// (Optional) The store where the transaction is taking place.
        /// </summary>
        public string StoreId { set { SetParameter("storeID", value); } }

        /// <summary>
        /// (Optional) Identifies the source of the transaction, e.g. "3rd party".
        /// </summary>
        public string StoreSourceId { set { SetParameter("storeSourceID", value); } }

        /// <summary>
        /// (Optional) Transaction receipt data as provided by the store, to be used for validation.
        /// </summary>
        public string TransactionReceipt { set { SetParameter("transactionReceipt", value); } }

        /// <summary>
        /// (Optional) The receipt signature from a Google Play purchase, to be used by the Google Play transaction validation process.
        /// </summary>
        public string TransactionReceiptSignature { set { SetParameter("transactionReceiptSignature", value); } }

        /// <summary>
        /// (Optional) The server to use for receipt verification, if applicable.
        /// </summary>
        public TransactionServer TransactionServer { set { SetParameter("transactionServer", k_TransactionServerValues[(int)value]); } }

        /// <summary>
        /// (Optional) An identifier for the person or entity with whom the transaction the occuring.
        /// For example, if this is a trade, this would be the other player's unique identifier.
        /// </summary>
        public string TransactorID { set { SetParameter("transactorID", value); } }

        /// <summary>
        /// (Optional) The real currency spent in this transaction.
        /// </summary>
        public TransactionRealCurrency SpentRealCurrency { get; set; }

        /// <summary>
        /// (Optional) The virtual currencies spent in this transaction.
        /// </summary>
        public List<TransactionVirtualCurrency> SpentVirtualCurrencies { get; private set; }

        /// <summary>
        /// (Optional) The items spent in this transaction.
        /// </summary>
        public List<TransactionItem> SpentItems { get; private set; }

        /// <summary>
        /// (Optional) The real currency received from this transaction.
        /// </summary>
        public TransactionRealCurrency ReceivedRealCurrency { get; set; }

        /// <summary>
        /// (Optional) The virtual currencies received from this transaction.
        /// </summary>
        public List<TransactionVirtualCurrency> ReceivedVirtualCurrencies { get; private set; }

        /// <summary>
        /// (Optional) The items received from this transaction.
        /// </summary>
        public List<TransactionItem> ReceivedItems { get; private set; }

        internal override void Serialize(IBuffer buffer)
        {
            buffer.PushString("sdkVersion", SdkVersion.SDK_VERSION);

            base.Serialize(buffer);

            buffer.PushProduct("productsReceived", ReceivedRealCurrency, ReceivedVirtualCurrencies, ReceivedItems);
            buffer.PushProduct("productsSpent", SpentRealCurrency, SpentVirtualCurrencies, SpentItems);
        }

        public override void Validate()
        {
            base.Validate();

            if (!ParameterHasBeenSet("transactionName"))
            {
                Debug.LogWarning("A value for the TransactionName parameter is required for a Transaction event.");
            }

            if (!ParameterHasBeenSet("transactionType"))
            {
                Debug.LogWarning("A value for the TransactionType parameter is required for a Transaction event.");
            }
        }

        public override void Reset()
        {
            base.Reset();

            SpentRealCurrency = null;
            SpentItems.Clear();
            SpentVirtualCurrencies.Clear();
            ReceivedRealCurrency = null;
            ReceivedItems.Clear();
            ReceivedVirtualCurrencies.Clear();
        }
    }
}
