using System.ComponentModel.DataAnnotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenRiaServices.Server;

namespace OpenRiaServices.Tools.Test
{
    using DescriptionAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute;

    [TestClass]
    public class CodeGenClientVisibleAttributeTests
    {
        [TestMethod]
        [Description("Property without [ClientVisible] should not generate [ClientVisible] attribute")]
        public void CodeGen_Attribute_ClientVisible_NotMarked_DoesNotGenerateAttribute()
        {
            string generatedCode = TestHelper.GenerateCodeAssertSuccess("C#", typeof(Mock_CG_Attr_ClientVisible_NotMarked_DomainService));

            TestHelper.AssertGeneratedCodeDoesNotContain(generatedCode, "ClientVisible");
        }

        [TestMethod]
        [Description("Property with [ClientVisible(true)] should generate [ClientVisible(true)] attribute")]
        public void CodeGen_Attribute_ClientVisible_True_GeneratesAttribute()
        {
            string generatedCode = TestHelper.GenerateCodeAssertSuccess("C#", typeof(Mock_CG_Attr_ClientVisible_True_DomainService));

            TestHelper.AssertGeneratedCodeContains(generatedCode, "[ClientVisible(true)]");
        }

        [TestMethod]
        [Description("Property with [ClientVisible(false)] should not generate [ClientVisible] attribute")]
        public void CodeGen_Attribute_ClientVisible_False_DoesNotGenerateAttribute()
        {
            string generatedCode = TestHelper.GenerateCodeAssertSuccess("C#", typeof(Mock_CG_Attr_ClientVisible_False_DomainService));

            TestHelper.AssertGeneratedCodeDoesNotContain(generatedCode, "ClientVisible");
        }

        [TestMethod]
        [Description("Multiple properties with different [ClientVisible] settings should generate correctly")]
        public void CodeGen_Attribute_ClientVisible_MultipleProperties_GeneratesCorrectly()
        {
            string generatedCode = TestHelper.GenerateCodeAssertSuccess("C#", typeof(Mock_CG_Attr_ClientVisible_Multiple_DomainService));

            TestHelper.AssertGeneratedCodeContains(generatedCode, "VisibleProperty");
            TestHelper.AssertGeneratedCodeContains(generatedCode, "InvisibleProperty");
            TestHelper.AssertGeneratedCodeContains(generatedCode, "NotMarkedProperty");

            int clientVisibleCount = CountOccurrences(generatedCode, "ClientVisible");
            Assert.AreEqual(1, clientVisibleCount, "Expected exactly one [ClientVisible] attribute");

            TestHelper.AssertGeneratedCodeContains(generatedCode, "public string VisibleProperty");
            TestHelper.AssertGeneratedCodeContains(generatedCode, "public string InvisibleProperty");
            TestHelper.AssertGeneratedCodeContains(generatedCode, "public string NotMarkedProperty");
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i)) != -1)
            {
                i += pattern.Length;
                count++;
            }
            return count;
        }
    }

    public class Mock_CG_Attr_ClientVisible_NotMarked_DomainService : GenericDomainService<Mock_CG_Attr_ClientVisible_NotMarked> { }

    public partial class Mock_CG_Attr_ClientVisible_NotMarked
    {
        [Key]
        public int KeyField { get; set; }

        public string StringProperty { get; set; }
    }

    public class Mock_CG_Attr_ClientVisible_True_DomainService : GenericDomainService<Mock_CG_Attr_ClientVisible_True> { }

    public partial class Mock_CG_Attr_ClientVisible_True
    {
        [Key]
        public int KeyField { get; set; }

        [ClientVisible(true)]
        public string VisibleProperty { get; set; }
    }

    public class Mock_CG_Attr_ClientVisible_False_DomainService : GenericDomainService<Mock_CG_Attr_ClientVisible_False> { }

    public partial class Mock_CG_Attr_ClientVisible_False
    {
        [Key]
        public int KeyField { get; set; }

        [ClientVisible(false)]
        public string InvisibleProperty { get; set; }
    }

    public class Mock_CG_Attr_ClientVisible_Multiple_DomainService : GenericDomainService<Mock_CG_Attr_ClientVisible_Multiple> { }

    public partial class Mock_CG_Attr_ClientVisible_Multiple
    {
        [Key]
        public int KeyField { get; set; }

        [ClientVisible(true)]
        public string VisibleProperty { get; set; }

        [ClientVisible(false)]
        public string InvisibleProperty { get; set; }

        public string NotMarkedProperty { get; set; }
    }
}
