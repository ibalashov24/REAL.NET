﻿using GraphX.PCL.Common.Enums;
using GraphX.PCL.Common.Interfaces;
using Repo;

namespace WPF_Editor.ViewModels.Helpers
{
    public class ModelNode : ModelElement, INode, IGraphXVertex
    {
        private INode _node;

        public ModelNode(INode node) : base(node)
        {
            _node = node;
        }

        #region IGraphXVertex implementation

        public bool Equals(IGraphXVertex other)
        {
            if (other is null)
                return false;
            return ID == other.ID;
        }

        public long ID { get; set; }
        public ProcessingOptionEnum SkipProcessing { get; set; }
        public double Angle { get; set; }
        public int GroupId { get; set; }

        #endregion
    }
}