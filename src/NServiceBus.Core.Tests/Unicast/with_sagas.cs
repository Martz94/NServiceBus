namespace NServiceBus.Unicast.Tests
{
    using System;
    using System.Collections.Generic;
    using Contexts;
    using InMemory.SagaPersister;
    using NServiceBus.Features;
    using Sagas;
    using NUnit.Framework;
    using Saga;

    class with_sagas : using_the_unicastBus
    {
        protected InMemorySagaPersister persister;
        Conventions conventions;
        Sagas sagas;

        [SetUp]
        public new void SetUp()
        {
            //todo
            persister = new InMemorySagaPersister(TypeBasedSagaMetaModel.Create(new List<Type>()));

            FuncBuilder.Register<ISagaPersister>(() => persister);

            sagas = new Sagas();

            FuncBuilder.Register<SagaConfigurationCache>(() => sagas.sagaConfigurationCache);

            conventions = new Conventions();
        }

        protected override void ApplyPipelineModifications()
        {
            pipelineModifications.Additions.Add(new SagaPersistenceBehavior.Registration());
        }

        protected void RegisterCustomFinder<T>() where T : IFinder
        {
            sagas.ConfigureFinder(typeof(T), conventions);
        }

        protected void RegisterSaga<T>(object sagaEntity = null) where T : new()
        {
        }

        static Type GetSagaEntityType<T>() where T : new()
        {
            var sagaType = typeof(T);


            var args = sagaType.BaseType.GetGenericArguments();
            foreach (var type in args)
            {
                if (typeof(IContainSagaData).IsAssignableFrom(type))
                {
                    return type;
                }
            }
            return null;
        }
    }
}