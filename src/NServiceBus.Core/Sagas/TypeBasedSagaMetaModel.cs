namespace NServiceBus.Features
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using NServiceBus.Saga;

    class TypeBasedSagaMetaModel:ISagaMetaModel
    {
        readonly Dictionary<string, SagaMetaData> metadata;

        public static ISagaMetaModel Create(IList<Type> availableTypes)
        {
            var metadata = new Dictionary<string,SagaMetaData>();


            return new TypeBasedSagaMetaModel(metadata);
        }
        internal static ISagaMetaModel Create<TSaga>() where TSaga : Saga
        {
            var metadata = new Dictionary<string, SagaMetaData>();

            var model = GenerateModel(typeof(TSaga));

            metadata[model.SagaEntityName] = model;
            return new TypeBasedSagaMetaModel(metadata);
        }

        internal static ISagaMetaModel CreateForEntity<TSaga>() where TSaga : IContainSagaData
        {
            var metadata = new Dictionary<string, SagaMetaData>();

            var model = GenerateModel(typeof(TSaga));

            metadata[model.SagaEntityName] = model;
             return new TypeBasedSagaMetaModel(metadata);
        }

        static SagaMetaData GenerateModel(Type sagaEntityType)
        {
            return new SagaMetaData();
            

        }

        private TypeBasedSagaMetaModel(Dictionary<string, SagaMetaData> metadata)
        {
            this.metadata = metadata;
        }   

        public SagaMetaData FindByName(string name)
        {
            return metadata[name];
        }
    }

    interface ISagaMetaModel
    {
        SagaMetaData FindByName(string name);
    }

    class SagaMetaData
    {
        public IEnumerable<string> UniqueProperties;
        public string SagaEntityName;
    }
}