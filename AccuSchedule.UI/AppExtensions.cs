using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml;

namespace AccuSchedule.UI
{
    public static class AppExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
        {
            if (items != null) foreach (var item in items) action(item);
        }

        public static Button SetContent(this Button btn, object content)
        {
            btn.Content = content;
            return btn;
        }

        public static XmlReader GetXMLTemplateFromXamlControl(Control ctrl)
        {
            var template = new StringReader(XamlWriter.Save(ctrl));
            return XmlReader.Create(template);
        }
        public static T ConvertXmlTemplateTo<T>(XmlReader xml)
        {
            T ret = (T)XamlReader.Load(xml);
            return ret;
        }


    }
}
