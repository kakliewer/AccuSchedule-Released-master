using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Methods
{
    public class TypedList<T> : List<T>, ITypedList, IList
        where T : ICustomTypeDescriptor
    {
        public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors)
        {
            if (this.Any())
            {
                return this[0].GetProperties();
            }
            return new PropertyDescriptorCollection(new PropertyDescriptor[0]);
        }

        public string GetListName(PropertyDescriptor[] listAccessors)
        {
            return null;
        }
    }



    public class SelectionWrapper<T> : DynamicObject, INotifyPropertyChanged, ICustomTypeDescriptor
    {
        private bool _IsSelected;
        public bool IsSelected
        {
            get { return _IsSelected; }
            set { SetProperty(ref _IsSelected, value); }
        }


        private T _Model;
        public T Model
        {
            get { return _Model; }
            set { SetProperty(ref _Model, value); }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (Model != null)
            {
                var prop = typeof(T).GetProperty(binder.Name);
                // indexer member will need parameters... not bothering with it
                if (prop != null && prop.CanRead && prop.GetMethod != null && prop.GetMethod.GetParameters().Length == 0)
                {
                    result = prop.GetValue(Model);
                    return true;
                }
            }
            return base.TryGetMember(binder, out result);
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            // not returning the Model property here
            return typeof(T).GetProperties().Select(x => x.Name).Concat(new[] { "IsSelected" });
        }

        public PropertyDescriptorCollection GetProperties()
        {
            var props = GetDynamicMemberNames();
            return new PropertyDescriptorCollection(props.Select(x => new DynamicPropertyDescriptor(x, GetType(), typeof(T))).ToArray());
        }

        // some INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChangedEvent([CallerMemberName]string prop = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(prop));
        }

        protected bool SetProperty<T2>(ref T2 store, T2 value, [CallerMemberName]string prop = null)
        {
            if (!object.Equals(store, value))
            {
                store = value;
                RaisePropertyChangedEvent(prop);
                return true;
            }
            return false;
        }

        public AttributeCollection GetAttributes()
        {
            throw new NotImplementedException();
        }

        public string GetClassName()
        {
            throw new NotImplementedException();
        }

        public string GetComponentName()
        {
            throw new NotImplementedException();
        }

        public TypeConverter GetConverter()
        {
            throw new NotImplementedException();
        }

        public EventDescriptor GetDefaultEvent()
        {
            throw new NotImplementedException();
        }

        public PropertyDescriptor GetDefaultProperty()
        {
            throw new NotImplementedException();
        }

        public object GetEditor(Type editorBaseType)
        {
            throw new NotImplementedException();
        }

        public EventDescriptorCollection GetEvents()
        {
            throw new NotImplementedException();
        }

        public EventDescriptorCollection GetEvents(Attribute[] attributes)
        {
            throw new NotImplementedException();
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            throw new NotImplementedException();
        }

        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            throw new NotImplementedException();
        }

    }





    public class DynamicPropertyDescriptor : PropertyDescriptor
    {
        private Type ObjectType;
        private System.Reflection.PropertyInfo Property;
        public DynamicPropertyDescriptor(string name, params Type[] objectType) : base(name, null)
        {
            ObjectType = objectType[0];
            foreach (var t in objectType)
            {
                Property = t.GetProperty(name);
                if (Property != null)
                {
                    break;
                }
            }
        }

        public override object GetValue(object component)
        {
            var prop = component.GetType().GetProperty(Name);
            if (prop != null)
            {
                return prop.GetValue(component);
            }
            DynamicObject obj = component as DynamicObject;
            if (obj != null)
            {
                var binder = new MyGetMemberBinder(Name);
                object value;
                obj.TryGetMember(binder, out value);
                return value;
            }
            return null;
        }

        public override void SetValue(object component, object value)
        {
            var prop = component.GetType().GetProperty(Name);
            if (prop != null)
            {
                prop.SetValue(component, value);
            }
            DynamicObject obj = component as DynamicObject;
            if (obj != null)
            {
                var binder = new MySetMemberBinder(Name);
                obj.TrySetMember(binder, value);
            }
        }

        public override Type PropertyType
        {
            get { return Property.PropertyType; }
        }

        public override bool IsReadOnly
        {
            get { return !Property.CanWrite; }
        }

        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override Type ComponentType
        {
            get { return typeof(object); }
        }

        public override void ResetValue(object component)
        {
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }
    }

    public class MyGetMemberBinder : GetMemberBinder
    {
        public MyGetMemberBinder(string name)
            : base(name, false)
        {

        }
        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
        {
            throw new NotImplementedException();
        }
    }
    public class MySetMemberBinder : SetMemberBinder
    {
        public MySetMemberBinder(string name)
            : base(name, false)
        {

        }
        public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
        {
            throw new NotImplementedException();
        }
    }

}
