namespace NServiceBus.Sagas
{
    using System.Collections.Generic;
    using System.Linq;

    class SagaMetaModel
    {
        internal SagaMetaModel(IEnumerable<SagaMetadata> foundSagas)
        {
            var sagas = foundSagas.ToList();

            foreach (var saga in sagas)
            {
                byEntityName[saga.EntityName] = saga;
            }

            foreach (var saga in sagas.SelectMany(s => s.AssociatedMessages).Distinct())
            {
                var messageType = saga.MessageType;

                byMessageType[messageType] = sagas.Where(s => s.AssociatedMessages.Any(m => m.MessageType == messageType)).ToList();
            }
        }

        public SagaMetadata FindByEntityName(string name)
        {
            return byEntityName[name];
        }

        public IEnumerable<SagaMetadata> All
        {
            get { return byEntityName.Values; }
        }

        public SagaMetadata FindByName(string name)
        {
            //todo - add a more efficient lookup
            return byEntityName.Values.Single(m => m.Name == name);
        }

        public IEnumerable<SagaMetadata> FindByMessageType(string messageType)
        {
            List<SagaMetadata> result;

            if(!byMessageType.TryGetValue(messageType,out result))
            {
                return new List<SagaMetadata>();
            }

            return result;
        }

        Dictionary<string,SagaMetadata> byEntityName = new Dictionary<string, SagaMetadata>();
        Dictionary<string, List<SagaMetadata>> byMessageType = new Dictionary<string, List<SagaMetadata>>(); 
    }
}