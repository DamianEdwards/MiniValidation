using System.ComponentModel;

namespace MiniValidation.UnitTests
{
    internal static class TypeDescriptorExtensions
    {
        public static void AttachAttribute(this Type type, string propertyName, Func<PropertyDescriptor, Attribute> attributeFactory)
        {
            var ctd = new PropertyOverridingTypeDescriptor(TypeDescriptor.GetProvider(type).GetTypeDescriptor(type)!);

            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(type))
            {
                if (pd.Name == propertyName)
                {
                    var pdWithAttribute = TypeDescriptor.CreateProperty(
                        type,
                        pd,
                        attributeFactory(pd));

                    ctd.OverrideProperty(pdWithAttribute);
                }
            }

            TypeDescriptor.AddProvider(new TypeDescriptorOverridingProvider(ctd), type);
        }
    }

    // From https://stackoverflow.com/questions/12143650/how-to-add-property-level-attribute-to-the-typedescriptor-at-runtime
    internal class PropertyOverridingTypeDescriptor : CustomTypeDescriptor
    {
        private readonly Dictionary<string, PropertyDescriptor> overridePds = new Dictionary<string, PropertyDescriptor>();

        public PropertyOverridingTypeDescriptor(ICustomTypeDescriptor parent)
            : base(parent)
        { }

        public void OverrideProperty(PropertyDescriptor pd)
        {
            overridePds[pd.Name] = pd;
        }

        public override object? GetPropertyOwner(PropertyDescriptor? pd)
        {
            var propertyOwner = base.GetPropertyOwner(pd);

            if (propertyOwner == null)
            {
                return this;
            }

            return propertyOwner;
        }

        public override PropertyDescriptorCollection GetProperties()
        {
            return GetPropertiesImpl(base.GetProperties());
        }

        public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
        {
            return GetPropertiesImpl(base.GetProperties(attributes));
        }

        private PropertyDescriptorCollection GetPropertiesImpl(PropertyDescriptorCollection pdc)
        {
            var pdl = new List<PropertyDescriptor>(pdc.Count + 1);

            foreach (PropertyDescriptor pd in pdc)
            {
                if (overridePds.ContainsKey(pd.Name))
                {
                    pdl.Add(overridePds[pd.Name]);
                }
                else
                {
                    pdl.Add(pd);
                }
            }

            var ret = new PropertyDescriptorCollection(pdl.ToArray());

            return ret;
        }
    }

    internal class TypeDescriptorOverridingProvider : TypeDescriptionProvider
    {
        private readonly ICustomTypeDescriptor ctd;

        public TypeDescriptorOverridingProvider(ICustomTypeDescriptor ctd)
        {
            this.ctd = ctd;
        }

        public override ICustomTypeDescriptor? GetTypeDescriptor(Type objectType, object? instance)
        {
            return ctd;
        }
    }
}
