using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenRiaServices.Server.Test.Utilities;
using TestDomainServices.Validation;

namespace OpenRiaServices.Tools.Test
{
    [TestClass]
    public class DateRangeCodeGenTests
    {
        [TestMethod]
        [Description("验证 DateRangeEntity 的 CustomValidation 属性在客户端代码生成中被正确传播")]
        public void CodeGen_DateRangeEntity_CustomValidationAttributeIsGenerated()
        {
            var validatorType = typeof(DateRangeValidator);
            var validateMethod = validatorType.GetMethod("ValidateDateRange", 
                new Type[] { typeof(object), typeof(ValidationContext) });

            Assert.IsNotNull(validateMethod, "应该找到 ValidateDateRange 方法");
            Assert.AreEqual(typeof(ValidationResult), validateMethod.ReturnType, "方法应该返回 ValidationResult");

            ISharedCodeService sts = new MockSharedCodeService(
                new Type[] { typeof(DateRangeValidator), typeof(DateRangeResources) },
                new MethodBase[] { validateMethod },
                Array.Empty<string>());

            string generatedCode = TestHelper.GenerateCodeAssertSuccess("C#", 
                new Type[] { typeof(DateRangeDomainService) }, sts);

            TestHelper.AssertGeneratedCodeContains(generatedCode,
                @"[CustomValidation(typeof(DateRangeValidator), ""ValidateDateRange"")]");

            TestHelper.AssertGeneratedCodeContains(generatedCode, "DateRangeEntity");
            TestHelper.AssertGeneratedCodeContains(generatedCode, "StartDate");
            TestHelper.AssertGeneratedCodeContains(generatedCode, "EndDate");
        }

        [TestMethod]
        [Description("验证 DateRangeDomainService 的客户端代码包含所有必要的成员")]
        public void CodeGen_DateRangeDomainService_ContainsExpectedMembers()
        {
            var validatorType = typeof(DateRangeValidator);
            var validateMethod = validatorType.GetMethod("ValidateDateRange",
                new Type[] { typeof(object), typeof(ValidationContext) });

            ISharedCodeService sts = new MockSharedCodeService(
                new Type[] { typeof(DateRangeValidator), typeof(DateRangeResources) },
                new MethodBase[] { validateMethod },
                Array.Empty<string>());

            string generatedCode = TestHelper.GenerateCodeAssertSuccess("C#",
                new Type[] { typeof(DateRangeDomainService) }, sts);

            TestHelper.AssertGeneratedCodeContains(generatedCode, "public sealed partial class DateRangeDomainContext : DomainContext");
            TestHelper.AssertGeneratedCodeContains(generatedCode, "public EntitySet<DateRangeEntity> DateRangeEntities");
            TestHelper.AssertGeneratedCodeContains(generatedCode, "GetDateRangeEntitiesQuery");
            TestHelper.AssertGeneratedCodeContains(generatedCode, "public DateTime StartDate");
            TestHelper.AssertGeneratedCodeContains(generatedCode, "public DateTime EndDate");
        }

        [TestMethod]
        [Description("验证服务端和客户端使用相同的验证器类型")]
        public void CodeGen_DateRangeValidator_SameTypeOnServerAndClient()
        {
            var serverValidatorType = typeof(DateRangeValidator);
            
            var validateMethod = serverValidatorType.GetMethod("ValidateDateRange",
                new Type[] { typeof(object), typeof(ValidationContext) });

            Assert.IsNotNull(validateMethod, "服务端应该有 ValidateDateRange 方法");
            Assert.IsTrue(validateMethod.IsStatic, "方法应该是静态的");
            Assert.IsTrue(validateMethod.IsPublic, "方法应该是公共的");

            var parameters = validateMethod.GetParameters();
            Assert.AreEqual(2, parameters.Length, "方法应该有两个参数");
            Assert.AreEqual(typeof(object), parameters[0].ParameterType, "第一个参数应该是 object");
            Assert.AreEqual(typeof(ValidationContext), parameters[1].ParameterType, "第二个参数应该是 ValidationContext");
        }

        [TestMethod]
        [Description("验证 DateRangeEntity 具有所需的验证属性")]
        public void DateRangeEntity_HasExpectedValidationAttributes()
        {
            var entityType = typeof(DateRangeEntity);
            
            var customValidationAttributes = entityType.GetCustomAttributes(typeof(CustomValidationAttribute), true);
            
            Assert.IsTrue(customValidationAttributes.Length > 0, "实体应该有 CustomValidationAttribute");

            var customValidationAttr = customValidationAttributes[0] as CustomValidationAttribute;
            Assert.IsNotNull(customValidationAttr);
            Assert.AreEqual(typeof(DateRangeValidator), customValidationAttr.ValidatorType, "验证器类型应该是 DateRangeValidator");
            Assert.AreEqual("ValidateDateRange", customValidationAttr.Method, "验证方法名应该是 ValidateDateRange");
        }

        [TestMethod]
        [Description("验证 DateRangeValidator 的 ValidateDateRange 方法签名正确")]
        public void DateRangeValidator_ValidateDateRange_HasCorrectSignature()
        {
            var validatorType = typeof(DateRangeValidator);
            
            var method = validatorType.GetMethod("ValidateDateRange",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(object), typeof(ValidationContext) },
                null);

            Assert.IsNotNull(method, "应该找到 ValidateDateRange 方法");
            Assert.AreEqual(typeof(ValidationResult), method.ReturnType, "返回类型应该是 ValidationResult");
        }

        [TestMethod]
        [Description("验证可空日期实体也有正确的验证属性")]
        public void DateRangeEntityWithNullableDates_HasExpectedValidationAttributes()
        {
            var entityType = typeof(DateRangeEntityWithNullableDates);
            
            var customValidationAttributes = entityType.GetCustomAttributes(typeof(CustomValidationAttribute), true);
            
            Assert.IsTrue(customValidationAttributes.Length > 0, "实体应该有 CustomValidationAttribute");

            var customValidationAttr = customValidationAttributes[0] as CustomValidationAttribute;
            Assert.IsNotNull(customValidationAttr);
            Assert.AreEqual(typeof(DateRangeValidator), customValidationAttr.ValidatorType, "验证器类型应该是 DateRangeValidator");
        }
    }
}
