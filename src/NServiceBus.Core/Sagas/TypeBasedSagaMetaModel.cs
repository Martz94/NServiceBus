namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Saga;

    class TypeBasedSagaMetaModel:ISagaMetaModel
    {
        readonly Dictionary<string, SagaMetaData> model = new Dictionary<string, SagaMetaData>();

        public static ISagaMetaModel Create(IList<Type> availableTypes)
        {
            return new TypeBasedSagaMetaModel(availableTypes.Where(t=>typeof(Saga).IsAssignableFrom(t))
                .Select(GenerateModel).ToList());
        }
        static SagaMetaData GenerateModel(Type sagaType)
        {
            var sagaEntityType = sagaType.BaseType.GetGenericArguments().Single();

            var uniquePropertiesOnEntity = FindUniqueAttributes(sagaEntityType);

            return new SagaMetaData
            {
                EntityName = sagaEntityType.FullName,
                UniqueProperties = uniquePropertiesOnEntity
            };
        }

        static IEnumerable<string> FindUniqueAttributes(Type sagaEntityType)
        {
            return UniqueAttribute.GetUniqueProperties(sagaEntityType).Select(pt => pt.Name);
        }

        private TypeBasedSagaMetaModel(List<SagaMetaData> metadata)
        {
            foreach (var sagaMetaData in metadata)
            {
                model[sagaMetaData.EntityName] = sagaMetaData;
            }
        }   

        public SagaMetaData FindByEntityName(string name)
        {
            return model[name];
        }

        public IEnumerable<SagaMetaData> All
        {
            get { return model.Values; }
        }
    }

    interface ISagaMetaModel
    {
        SagaMetaData FindByEntityName(string name);

        IEnumerable<SagaMetaData> All { get; } 
    }

    class SagaMetaData
    {
        public IEnumerable<string> UniqueProperties;
        public string EntityName;
    }
}