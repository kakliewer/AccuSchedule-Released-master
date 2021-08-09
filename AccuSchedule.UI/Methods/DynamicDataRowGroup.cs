using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Methods
{
    public sealed class DynamicDataRowGroup : DynamicObject, ICustomTypeDescriptor, IEquatable<DynamicDataRowGroup>
    {
        private readonly DataRow _row;
        private readonly ISet<string> _columns;
        private readonly PropertyDescriptorCollection _properties;

        public DynamicDataRowGroup(DataRow row, IEnumerable<string> columns)
        {
            if (row == null) throw new ArgumentNullException("row");
            if (columns == null) throw new ArgumentNullException("columns");

            _row = row;
            _columns = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);

            var properties = _columns.Select(name => DynamicDataRowGroupProperty.Create(row, name));
            _properties = new PropertyDescriptorCollection(properties.ToArray<PropertyDescriptor>(), true);
        }

        public DynamicDataRowGroup(DataRow row, params string[] columns) : this(row, columns.AsEnumerable())
        {
        }

        public override int GetHashCode()
        {
            int result = 0;
            foreach (string column in _columns)
            {
                object value = _row[column];
                int code = (value == null) ? 0 : value.GetHashCode();
                result = unchecked((result * 397) + code);
            }

            return result;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DynamicDataRowGroup);
        }

        public bool Equals(DynamicDataRowGroup other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(other, this)) return true;
            if (!_columns.SetEquals(other._columns)) return false;
            return _columns.All(c => Equals(_row[c], other._row[c]));
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return _columns;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (_columns.Contains(binder.Name))
            {
                result = _row[binder.Name];
                return true;
            }

            return base.TryGetMember(binder, out result);
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (indexes != null && indexes.Length == 1)
            {
                string name = indexes[0] as string;
                if (name != null && _columns.Contains(name))
                {
                    result = _row[name];
                    return true;
                }
            }

            return base.TryGetIndex(binder, indexes, out result);
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
        {
            return _properties;
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes)
        {
            return _properties;
        }

        object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }

        PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty()
        {
            return null;
        }

        AttributeCollection ICustomTypeDescriptor.GetAttributes()
        {
            return AttributeCollection.Empty;
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents()
        {
            return EventDescriptorCollection.Empty;
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes)
        {
            return EventDescriptorCollection.Empty;
        }

        EventDescriptor ICustomTypeDescriptor.GetDefaultEvent()
        {
            return null;
        }

        string ICustomTypeDescriptor.GetClassName()
        {
            return null;
        }

        string ICustomTypeDescriptor.GetComponentName()
        {
            return null;
        }

        TypeConverter ICustomTypeDescriptor.GetConverter()
        {
            return null;
        }

        object ICustomTypeDescriptor.GetEditor(Type editorBaseType)
        {
            return null;
        }

        private sealed class DynamicDataRowGroupProperty : PropertyDescriptor
        {
            private static readonly Attribute[] EmptyAttributes = new Attribute[0];
            private readonly Type _propertyType;

            private DynamicDataRowGroupProperty(string name, Type propertyType) : base(name, EmptyAttributes)
            {
                _propertyType = propertyType;
            }

            public override Type ComponentType
            {
                get { return typeof(DynamicDataRowGroup); }
            }

            public override Type PropertyType
            {
                get { return _propertyType; }
            }

            public override bool IsReadOnly
            {
                get { return true; }
            }

            public override object GetValue(object component)
            {
                var group = component as DynamicDataRowGroup;
                return (group == null) ? null : group._row[Name];
            }

            public override bool ShouldSerializeValue(object component)
            {
                return false;
            }

            public override bool CanResetValue(object component)
            {
                return false;
            }

            public override void ResetValue(object component)
            {
                throw new NotSupportedException();
            }

            public override void SetValue(object component, object value)
            {
                throw new NotSupportedException();
            }

            public static DynamicDataRowGroupProperty Create(DataRow row, string name)
            {
                DataColumn column = row.Table.Columns[name];
                if (column == null) throw new ArgumentException(string.Format("Column '{0}' was not found.", name));
                return new DynamicDataRowGroupProperty(name, column.DataType);
            }
        }
    }
}
