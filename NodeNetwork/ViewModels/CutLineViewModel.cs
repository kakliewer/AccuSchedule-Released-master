using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using DynamicData;
using ReactiveUI;

namespace NodeNetwork.ViewModels
{
    [Serializable]
    [DataContract]
    /// <summary>
    /// Viewmodel class for the UI cutting line that is used to delete connections.
    /// </summary>
    public class CutLineViewModel : ReactiveObject
    {
        #region StartPoint
        [DataMember]
        /// <summary>
        /// The coordinates of the point at which the cutting line starts.
        /// </summary>
        public Point StartPoint
        {
            get => _startPoint;
            set => this.RaiseAndSetIfChanged(ref _startPoint, value);
        }
        [IgnoreDataMember]
        private Point _startPoint;
        #endregion

        #region EndPoint
        [DataMember]
        /// <summary>
        /// The coordinates of the point at which the cutting line ends.
        /// </summary>
        public Point EndPoint
        {
            get => _endPoint;
            set => this.RaiseAndSetIfChanged(ref _endPoint, value);
        }
        [IgnoreDataMember]
        private Point _endPoint;
        #endregion

        #region IsVisible
        [DataMember]
        /// <summary>
        /// If true, the cutting line is visible. If false, the cutting line is hidden.
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => this.RaiseAndSetIfChanged(ref _isVisible, value);
        }
        [IgnoreDataMember]
        private bool _isVisible;
        #endregion

        #region IntersectingConnections
        [DataMember]
        /// <summary>
        /// A list of connections that visually intersect with the cutting line.
        /// This list is driven by the view.
        /// </summary>
        public ISourceList<ConnectionViewModel> IntersectingConnections { get; } = new SourceList<ConnectionViewModel>();
        #endregion
    }
}
