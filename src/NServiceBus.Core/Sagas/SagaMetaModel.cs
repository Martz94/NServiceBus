namespace NServiceBus.Sagas
{
    using System.Collections.Generic;
    using System.Linq;

    class SagaMetaModel
    {
        public SagaMetaModel(IEnumerable<SagaMetadata> foundSagas)
        {
            var sagas = foundSagas.ToList();

            foreach (var saga in sagas)
            {
                byEntityName[saga.EntityName] = saga;
            }

            foreach (var saga in sagas)
            {
                byName[saga.Name] = saga;
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
            return byName[name];
        }

        Dictionary<string,SagaMetadata> byEntityName = new Dictionary<string, SagaMetadata>();
        Dictionary<string, SagaMetadata> byName = new Dictionary<string, SagaMetadata>(); 
    }
}