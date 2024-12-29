namespace Unity.Services.Analytics
{
    public interface IAnalyticsService
    {
        /// <summary>
        /// This is the URL for the Unity Analytics privacy policy. This policy page should
        /// be presented to the user in a platform-appropriate way along with the ability to
        /// opt out of data collection.
        /// </summary>
        string PrivacyUrl { get; }

        /// <summary>
        /// Signals that consent has been obtained from the player and enables data collection.
        ///
        /// By calling this method you confirm that consent has been obtained or is not required from the player under any applicable
        /// data privacy laws (e.g. GDPR in Europe, PIPL in China). Please obtain your own legal advice to ensure you are in compliance
        /// with any data privacy laws regarding personal data collection in the territories in which your app is available.
        /// </summary>
        void StartDataCollection();

        /// <summary>
        /// Gets the user ID that Analytics is currently recording into the userId field of events.
        /// </summary>
        /// <returns>The user ID as a string</returns>
        string GetAnalyticsUserID();

        /// <summary>
        /// Gets the session ID that is currently recording into the sessionID field of events.
        /// </summary>
        /// <returns>The session ID as a string</returns>
        string SessionID { get; }

        /// <summary>
        /// Converts an amount of currency to the minor units required for the objects passed to the Transaction method.
        /// This method uses data from ISO 4217. Note that this method expects you to pass in currency in the major units for
        /// conversion - if you already have data in the minor units you don't need to call this method.
        /// For example - 1.99 USD would be converted to 199, 123 JPY would be returned unchanged.
        /// </summary>
        /// <param name="currencyCode">The ISO4217 currency code for the input currency. For example, USD for dollars, or JPY for Japanese Yen</param>
        /// <param name="value">The major unit value of currency, for example 1.99 for 1 dollar 99 cents.</param>
        /// <returns>The minor unit value of the input currency, for example for an input of 1.99 USD 199 would be returned.</returns>
        long ConvertCurrencyToMinorUnits(string currencyCode, double value);

        /// <summary>
        /// Record an event, if the player has opted in to data collection (see StartDataCollection method).
        ///
        /// Once the event has been serialized, the Event instance will be cleared so it can be safely reused.
        ///
        /// A schema for this event must exist on the dashboard or it will be ignored.
        /// </summary>
        /// <param name="e">(Required) The event to be recorded.</param>
        void RecordEvent(Event e);

        /// <summary>
        /// Record an event that has no parameters, if the player has opted in to data collection (see StartDataCollection method).
        ///
        /// A schema for this event must exist on the dashboard or it will be ignored.
        /// </summary>
        /// <param name="e">(Required) The name of the event to be recorded.</param>
        void RecordEvent(string eventName);

        /// <summary>
        /// Forces an immediately upload of all recorded events to the server, if there is an internet connection and a flush is not already in progress.
        /// Flushing is triggered automatically on a regular cadence so you should not need to use this method, unless you specifically require some
        /// queued events to be uploaded immediately.
        /// </summary>
        /// <exception cref="ConsentCheckException">Thrown if the required consent flow cannot be determined..</exception>
        void Flush();

        /// <summary>
        /// Disables data collection, preventing any further events from being recorded or uploaded.
        /// A final upload containing any events that are currently buffered will be attempted.
        ///
        /// Data collection can be re-enabled later, by calling the StartDataCollection method.
        /// </summary>
        void StopDataCollection();

        /// <summary>
        /// Requests that all historic data for this user be purged from the back-end and disables data collection.
        /// This can be called regardless of whether data collection is currently enabled or disabled.
        ///
        /// If the purge request fails (e.g. due to the client being offline), it will be retried until it is successful, even
        /// across multiple sessions if necessary.
        /// </summary>
        void RequestDataDeletion();
    }
}
