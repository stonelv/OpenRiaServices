using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenRiaServices.Client.Internal;

namespace OpenRiaServices.Client.Test.Reflection
{
    [TestClass]
    public class MetaTypeTests
    {
        [TestMethod]
        public void ShouldIgnorePropertiesWithIgnoreDataMember()
        {
            var metaType = MetaType.GetMetaType(typeof(EntityWithIgnoreProperty));
            var entity = new EntityWithIgnoreProperty()
            {
                Id = 1,
                IgnoredProperty = "ignored",
            };

            Assert.AreEqual(1, metaType.DataMembers.Count(), "DataMembers should not include ignored members");
            Assert.AreEqual(nameof(EntityWithIgnoreProperty.Id), metaType.DataMembers.First().Name);

            var state = ObjectStateUtility.ExtractState(entity);
            Assert.HasCount(1, state, "Extract state should only include non ignored properties");

            ObjectStateUtility.ApplyState(entity, new Dictionary<string, object>
            {
                { nameof(EntityWithIgnoreProperty.Id), (object)2},
                { nameof(EntityWithIgnoreProperty.IgnoredProperty), null},
            });
            Assert.AreEqual(2, entity.Id, "ApplyState should not change ignored properties");
            Assert.IsNotNull(entity.IgnoredProperty, "ApplyState should not change ignored properties");
        }

        [TestMethod]
        public void ShouldIgnorePropertiesWithComputedAttribute()
        {
            var metaType = MetaType.GetMetaType(typeof(EntityWithComputedProperty));
            var entity = new EntityWithComputedProperty()
            {
                Id = 1,
                FirstName = "John",
                LastName = "Doe",
                DisplayName = "John Doe",
                CalculatedValue = 100,
            };

            Assert.AreEqual(3, metaType.DataMembers.Count(), "DataMembers should not include computed members");
            Assert.IsTrue(metaType.DataMembers.Any(m => m.Name == nameof(EntityWithComputedProperty.Id)), "Id should be a data member");
            Assert.IsTrue(metaType.DataMembers.Any(m => m.Name == nameof(EntityWithComputedProperty.FirstName)), "FirstName should be a data member");
            Assert.IsTrue(metaType.DataMembers.Any(m => m.Name == nameof(EntityWithComputedProperty.LastName)), "LastName should be a data member");
            Assert.IsFalse(metaType.DataMembers.Any(m => m.Name == nameof(EntityWithComputedProperty.FullName)), "FullName should not be a data member");
            Assert.IsFalse(metaType.DataMembers.Any(m => m.Name == nameof(EntityWithComputedProperty.DisplayName)), "DisplayName should not be a data member (even with getter and setter)");
            Assert.IsFalse(metaType.DataMembers.Any(m => m.Name == nameof(EntityWithComputedProperty.CalculatedValue)), "CalculatedValue should not be a data member (even with getter and setter)");

            var state = ObjectStateUtility.ExtractState(entity);
            Assert.HasCount(3, state, "Extract state should only include non computed properties");
            Assert.IsTrue(state.ContainsKey(nameof(EntityWithComputedProperty.Id)), "ExtractState should include Id");
            Assert.IsTrue(state.ContainsKey(nameof(EntityWithComputedProperty.FirstName)), "ExtractState should include FirstName");
            Assert.IsTrue(state.ContainsKey(nameof(EntityWithComputedProperty.LastName)), "ExtractState should include LastName");
            Assert.IsFalse(state.ContainsKey(nameof(EntityWithComputedProperty.FullName)), "ExtractState should not include FullName");
            Assert.IsFalse(state.ContainsKey(nameof(EntityWithComputedProperty.DisplayName)), "ExtractState should not include DisplayName");
            Assert.IsFalse(state.ContainsKey(nameof(EntityWithComputedProperty.CalculatedValue)), "ExtractState should not include CalculatedValue");
        }

        public class EntityWithIgnoreProperty : Entity
        {
            public int Id { get; set; }

            [IgnoreDataMember]
            public string IgnoredProperty { get; set; }

            [IgnoreDataMember]
            public string ThrowingProperty { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }

        public class EntityWithComputedProperty : Entity
        {
            public int Id { get; set; }
            
            public string FirstName { get; set; }
            
            public string LastName { get; set; }

            [Computed]
            public string FullName => $"{FirstName} {LastName}";

            private string _displayName;
            
            [Computed]
            public string DisplayName
            {
                get => _displayName;
                set => _displayName = value;
            }

            private int _calculatedValue;
            
            [Computed]
            public int CalculatedValue
            {
                get => _calculatedValue;
                set => _calculatedValue = value;
            }
        }
    }
}
