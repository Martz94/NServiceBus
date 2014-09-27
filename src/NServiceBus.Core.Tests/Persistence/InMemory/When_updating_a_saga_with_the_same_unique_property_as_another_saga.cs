namespace NServiceBus.SagaPersisters.InMemory.Tests
{
    using System;
    using NServiceBus.Features;
    using NServiceBus.InMemory.SagaPersister;
    using NUnit.Framework;

    [TestFixture]
    public class When_updating_a_saga_with_the_same_unique_property_as_another_saga
    {
  
        [Test]
        public void It_should_persist_successfully()
        {
            var saga1 = new SagaWithUniquePropertyData {Id = Guid.NewGuid(), UniqueString = "whatever1"};
            var saga2 = new SagaWithUniquePropertyData {Id = Guid.NewGuid(), UniqueString = "whatever"};
  
            var inMemorySagaPersister = new InMemorySagaPersister(TypeBasedSagaMetaModel.Create<SagaWithUniqueProperty>());
              
            inMemorySagaPersister.Save(saga1);
            inMemorySagaPersister.Save(saga2);

            Assert.Throws<InvalidOperationException>(() => 
            {
                var saga = inMemorySagaPersister.Get<SagaWithUniquePropertyData>(saga2.Id);
                saga.UniqueString = "whatever1";
                inMemorySagaPersister.Update(saga);
            });
        }

        [Test]
        public void It_should_persist_successfully_for_two_unique_properties()
        {
            var saga1 = new SagaWithTwoUniquePropertiesData { Id = Guid.NewGuid(), UniqueString = "whatever1", UniqueInt = 5};
            var saga2 = new SagaWithTwoUniquePropertiesData { Id = Guid.NewGuid(), UniqueString = "whatever", UniqueInt = 37};
            var inMemorySagaPersister = new InMemorySagaPersister(TypeBasedSagaMetaModel.Create<SagaWithTwoUniqueProperties>());
  
            inMemorySagaPersister.Save(saga1);
            inMemorySagaPersister.Save(saga2);

            Assert.Throws<InvalidOperationException>(() =>
            {
                var saga = inMemorySagaPersister.Get<SagaWithTwoUniquePropertiesData>(saga2.Id);
                saga.UniqueInt = 5;
                inMemorySagaPersister.Update(saga);
            });
        }
    }
}