﻿namespace NServiceBus.Core.Tests.Sagas.TypeBasedSagas
{
    using System;
    using System.Linq;
    using NServiceBus.Features;
    using NServiceBus.Saga;
    using NServiceBus.Sagas.Finders;
    using NUnit.Framework;

    [TestFixture]
    public class TypeBasedSagaMetaModelTests
    {
        [Test]
        public void FindSagasByName()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySaga) });

            var metadata = model.FindByName(typeof(MySaga).FullName);


            Assert.NotNull(metadata);
        }
        [Test]
        public void FindSagasByEntityName()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySaga) });

            var metadata = model.FindByEntityName(typeof(MyEntity).FullName);


            Assert.NotNull(metadata);
        }

        [Test]
        public void GetEntityClrType()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySaga) });

            var metadata = model.FindByEntityName(typeof(MyEntity).FullName);


            Assert.AreEqual(typeof(MyEntity), metadata.Properties["entity-clr-type"]);
        }

        [Test]
        public void GetSagaClrType()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySaga) });

            var metadata = model.FindByEntityName(typeof(MyEntity).FullName);


            Assert.AreEqual(typeof(MySaga), metadata.Properties["saga-clr-type"]);
        }

        [Test]
        public void DetectUniquePropertiesByAttribute()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySaga) });

            var metadata = model.FindByEntityName(typeof(MyEntity).FullName);


            Assert.AreEqual("UniqueProperty", metadata.UniqueProperties.Single());
        }

        [Test]
        public void AutomaticallyAddUniqueForMappedProperties()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySagaWithMappedProperty) });

            var metadata = model.FindByEntityName(typeof(MySagaWithMappedProperty.SagaData).FullName);


            Assert.AreEqual("UniqueProperty", metadata.UniqueProperties.Single());
        }

        [Test]
        public void RequireFinderForMessagesStartingTheSaga()
        {
            var ex = Assert.Throws<Exception>(() => TypeBasedSagaMetaModel.Create(typeof(MySagaWithUnmappedStartProperty)));


            Assert.True(ex.Message.Contains(typeof(MessageThatStartsTheSaga).FullName));
        }

        [Test]
        public void DetectMessagesStartingTheSaga()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(SagaWith2StartersAnd1Handler) });

            var metadata = model.FindByName(typeof(SagaWith2StartersAnd1Handler).FullName);

            var messages = metadata.AssociatedMessages;

            Assert.AreEqual(3, messages.Count());

            Assert.True(metadata.IsMessageAllowedToStartTheSaga(typeof(SagaWith2StartersAnd1Handler.StartMessage1).FullName));

            Assert.True(metadata.IsMessageAllowedToStartTheSaga(typeof(SagaWith2StartersAnd1Handler.StartMessage2).FullName));

            Assert.False(metadata.IsMessageAllowedToStartTheSaga(typeof(SagaWith2StartersAnd1Handler.Message3).FullName));
        }

        [Test]
        public void DetectAndRegisterPropertyFinders()
        {
            var metadata = TypeBasedSagaMetaModel.Create(typeof(MySagaWithMappedProperty));

            var finder = metadata.GetFinder(typeof(SomeMessage).FullName);

            Assert.AreEqual(typeof(PropertySagaFinder<MySagaWithMappedProperty.SagaData>), finder.Type);
            Assert.NotNull(finder.Properties["property-accessor"]);
            Assert.AreEqual("UniqueProperty", finder.Properties["saga-property-name"]);
        }

        [Test]
        public void FilterOutNonSagaTypes()
        {
            Assert.AreEqual(1, TypeBasedSagaMetaModel.Create(new[] { typeof(MySaga), typeof(string) }).All.Count());
        }

        class MySagaWithMappedProperty : Saga<MySagaWithMappedProperty.SagaData>
        {
            public class SagaData : ContainSagaData
            {
                public int UniqueProperty { get; set; }
            }
            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper)
            {
                mapper.ConfigureMapping<SomeMessage>(m => m.SomeProperty)
                    .ToSaga(s => s.UniqueProperty);
            }
        }


        class SagaWith2StartersAnd1Handler : Saga<SagaWith2StartersAnd1Handler.SagaData>,
            IAmStartedByMessages<SagaWith2StartersAnd1Handler.StartMessage1>,
            IAmStartedByMessages<SagaWith2StartersAnd1Handler.StartMessage2>,
            IHandleMessages<SagaWith2StartersAnd1Handler.Message3>
        {

            public class StartMessage1 : IMessage {
                public string SomeId { get; set; }
            }
            public class StartMessage2 : IMessage {
                public string SomeId { get; set; }
            }

            public class Message3 : IMessage { }
            public class SagaData : ContainSagaData {
                public string SomeId { get; set; }
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper)
            {
                mapper.ConfigureMapping<StartMessage1>(m=>m.SomeId)
                    .ToSaga(s=>s.SomeId);
                mapper.ConfigureMapping<StartMessage2>(m => m.SomeId)
                    .ToSaga(s => s.SomeId);
            }

            public void Handle(StartMessage1 message)
            {
                throw new NotImplementedException();
            }

            public void Handle(StartMessage2 message)
            {
                throw new NotImplementedException();
            }

            public void Handle(Message3 message)
            {
                throw new NotImplementedException();
            }
        }


        class MySaga : Saga<MyEntity>
        {
            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MyEntity> mapper)
            {

            }
        }

        class MyEntity : ContainSagaData
        {
            [Unique]
            public int UniqueProperty { get; set; }
        }

        class MySagaWithUnmappedStartProperty : Saga<MySagaWithUnmappedStartProperty.SagaData>,
            IAmStartedByMessages<MessageThatStartsTheSaga>,
            IHandleMessages<MessageThatDoesntStartTheSaga>
        {


            public class SagaData : ContainSagaData
            {

            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper)
            {


            }

            public void Handle(MessageThatStartsTheSaga message)
            {


            }

            public void Handle(MessageThatDoesntStartTheSaga message)
            {

            }
        }
    }

    class SomeMessage
    {
        public int SomeProperty { get; set; }
    }

    class MessageThatDoesntStartTheSaga
    {
        public int SomeProperty { get; set; }
    }


    class MessageThatStartsTheSaga
    {
        public int SomeProperty { get; set; }
    }
}