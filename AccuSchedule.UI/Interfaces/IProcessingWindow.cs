using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Interfaces
{
    public interface IProcessingWindow
    {
        DataSet Data { get; set; }
        MetroWindow ProcessingWindow { get; } 
        void AddToDataSet(DataTable Datatable, string FileName);
        
    }
}
