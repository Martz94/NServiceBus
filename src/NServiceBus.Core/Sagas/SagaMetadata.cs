namespace NServiceBus.Sagas
{
    using System.Collections.Generic;

    /// <summary>
    /// Contains metadata for known sagas
    /// </summary>
    public class SagaMetadata
    {
        internal SagaMetadata(IEnumerable<SagaMessage> messages, IEnumerable<SagaFinderDefinition> finders)
        {
            Properties = new Dictionary<string, object>();
            UniqueProperties = new List<string>();

            associatedMessages = new Dictionary<string, SagaMessage>();

            foreach (var sagaMessage in messages)
            {
                associatedMessages[sagaMessage.MessageType] = sagaMessage;
            }


            sagaFinders = new Dictionary<string, SagaFinderDefinition>();

            foreach (var finder in finders)
            {
                sagaFinders[finder.MessageType] = finder;
            }

        }

        Dictionary<string, SagaMessage> associatedMessages;
        Dictionary<string, SagaFinderDefinition> sagaFinders;
        /// <summary>
        /// Unique properties for this saga
        /// </summary>
        public IEnumerable<string> UniqueProperties;

        /// <summary>
        /// The name of the saga
        /// </summary>
        public string Name;

        /// <summary>
        /// List of related properties
        /// </summary>
        public Dictionary<string, object> Properties;

        /// <summary>
        /// The name of the saga data entity
        /// </summary>
        public string EntityName;

        /// <summary>
        /// True if the specified message type is allowed to start the saga
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        public bool IsMessageAllowedToStartTheSaga(string messageType)
        {
            SagaMessage sagaMessage;

            if (!associatedMessages.TryGetValue(messageType, out sagaMessage))
            {
                return false;
            }
            return sagaMessage.IsAllowedToStartSaga;
        }

        /// <summary>
        /// Returns the list of messages that is associated with this saga
        /// </summary>
        public IEnumerable<SagaMessage> AssociatedMessages
        {
            get { return associatedMessages.Values; }
        }

        /// <summary>
        /// Gets the list of finders for this saga
        /// </summary>
        public IEnumerable<SagaFinderDefinition> Finders
        {
            get { return sagaFinders.Values; }
        }

        

        /// <summary>
        /// Gets the configured finder for this message
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="finderDefinition">The finder if present</param>
        /// <returns>True if finder exists</returns>
        public bool TryGetFinder(string messageType,out SagaFinderDefinition finderDefinition)
        {
            return sagaFinders.TryGetValue(messageType,out finderDefinition);
        }

    }
}