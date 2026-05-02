using System;
using OpenRiaServices.Server;

namespace OpenRiaServices.Tools
{
    /// <summary>
    /// Custom attribute builder generates <see cref="AttributeDeclaration"/> representations of
    /// <see cref="ClientVisibleAttribute"/> instances.
    /// This builder converts server-side ClientVisibleAttribute to client-side ClientVisibleAttribute.
    /// </summary>
    internal class ClientVisibleAttributeBuilder : ICustomAttributeBuilder
    {
        /// <summary>
        /// Generates a <see cref="AttributeDeclaration"/> representation of an 
        /// <see cref="ClientVisibleAttribute"/> instance.
        /// </summary>
        public AttributeDeclaration GetAttributeDeclaration(Attribute attribute)
        {
            if (attribute is OpenRiaServices.Server.ClientVisibleAttribute clientVisibleAttr)
            {
                if (!clientVisibleAttr.IsVisible)
                {
                    return null;
                }
                
                AttributeDeclaration attributeDeclaration = new AttributeDeclaration(typeof(OpenRiaServices.Client.ClientVisibleAttribute));
                attributeDeclaration.ConstructorArguments.Add(clientVisibleAttr.IsVisible);
                return attributeDeclaration;
            }
            
            return null;
        }
    }
}
