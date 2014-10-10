namespace NServiceBus.Unicast.Tests
{
    using System;
    using Contexts;
    using InMemory.SagaPersister;
    using NServiceBus.Sagas;
    using NUnit.Framework;
    using Saga;

    class with_sagas : using_the_unicastBus
    {
        protected InMemorySagaPersister persister = null;

        [SetUp]
        public new void SetUp()
        {
            FuncBuilder.Register<ISagaPersister>(() => persister);
        }

        protected override void ApplyPipelineModifications()
        {
            pipelineModifications.Additions.Add(new SagaPersistenceBehavior.Registration());
        }

        protected void RegisterCustomFinder<T>() where T : IFinder
        {
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