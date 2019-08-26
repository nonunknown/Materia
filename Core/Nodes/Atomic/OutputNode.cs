﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Materia.Imaging;
using Materia.Nodes.Attributes;
using Newtonsoft.Json;
using Materia.Textures;
using Materia.Imaging.GLProcessing;
using System.Threading;

namespace Materia.Nodes.Atomic
{
    public enum OutputType
    {
        basecolor,
        height,
        occlusion,
        roughness,
        metallic,
        normal,
        thickness,
        emission
    }

    /// <summary>
    /// An output node simply takes in 
    /// an input to distribute to other graphs
    /// or to export the final texture
    /// they can only have one input
    /// and no actual outputs
    /// </summary>
    public class OutputNode : ImageNode
    {
        NodeInput input;

        OutputType outtype;
        [Editable(ParameterInputType.Dropdown, "Out Type")]
        public OutputType OutType
        {
            get
            {
                return outtype;
            }
            set
            {
                outtype = value;
            }
        }

       
        public new int Height
        {
            get
            {
                return height;
            }
            set
            {
                height = value;
            }
        }

   
        public new int Width
        {
            get
            {
                return width;
            }
            set
            {
                width = value;
            }
        }

   
        public new float TileX
        {
            get
            {
                return tileX;
            }
            set
            {
                tileX = value;
            }
        }


        public new float TileY
        {
            get
            {
                return tileY;
            }
            set
            {
                tileY = value;
            }
        }

        NodeOutput Output;

        public OutputNode(GraphPixelType p = GraphPixelType.RGBA)
        {
            OutType = OutputType.basecolor;

            Name = "Output";

            Id = Guid.NewGuid().ToString();

            width = height = 16;
            tileX = tileY = 1;

            internalPixelType = p;

            previewProcessor = new BasicImageRenderer();

            input = new NodeInput(NodeType.Color | NodeType.Gray, this);
            Inputs = new List<NodeInput>();
            Inputs.Add(input);

            input.OnInputAdded += Input_OnInputAdded;
            input.OnInputChanged += Input_OnInputChanged;
            input.OnInputRemoved += Input_OnInputRemoved;

            Outputs = new List<NodeOutput>();
        }

        public void SetOutput(NodeOutput op)
        {
            Outputs.Clear();
            Output = op;
            Outputs.Add(op);
            TryAndProcess();
        }

        private void Input_OnInputRemoved(NodeInput n)
        {
            if(Output != null)
            {
                Output.Data = null;
                Output.Changed();
            }
        }

        private void Input_OnInputChanged(NodeInput n)
        {
            TryAndProcess();
        }

        private void Input_OnInputAdded(NodeInput n)
        {
            TryAndProcess();
        }

        public override void TryAndProcess()
        {
            if(!Async)
            {
                if (input != null && input.HasInput)
                {
                    Process();
                }

                return;
            }

            if (input != null && input.HasInput)
            {
                if (ParentGraph != null)
                {
                    ParentGraph.Schedule(this);
                }
            }
        }

        public override Task GetTask()
        {
            return Task.Factory.StartNew(() =>
            {

            })
            .ContinueWith(t =>
            {
                if(input != null && input.HasInput)
                {
                    Process();
                }
            }, Context);
        }

        void Process()
        {
            GLTextuer2D i1 = (GLTextuer2D)input.Input.Data;

            if (i1 == null) return;
            if (i1.Id == 0) return;

            height = i1.Height;
            width = i1.Width;

            if (Output != null)
            {
                Output.Data = i1;
                Output.Changed();
            }

            Updated();
        }

        public override GLTextuer2D GetActiveBuffer()
        {
            if(input != null && input.HasInput && input.Input.Data != null)
            {
                return input.Input.Node.GetActiveBuffer();
            }

            return null;
        }

        public override byte[] GetPreview(int width, int height)
        {
            GLTextuer2D active = GetActiveBuffer();

            if (active == null) return null;
            if (active.Id == 0) return null;

            previewProcessor.Process(width, height, active);
            byte[] bits = previewProcessor.ReadByte(width, height);
            previewProcessor.Complete();
            return bits;
        }

        public class OutputNodeData : NodeData
        {
            public OutputType outType;
        }

        public override void FromJson(string data)
        {
            OutputNodeData d = JsonConvert.DeserializeObject<OutputNodeData>(data);
            SetBaseNodeDate(d);
            outtype = d.outType;
        }

        public override string GetJson()
        {
            OutputNodeData d = new OutputNodeData();
            FillBaseNodeData(d);
            d.outputs = new List<NodeConnection>();
            d.outType = OutType;

            return JsonConvert.SerializeObject(d);
        }

        protected override void OnWidthHeightSet()
        {
            
        }
    }
}
