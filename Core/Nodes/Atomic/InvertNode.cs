﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Materia.Imaging.GLProcessing;
using Materia.Textures;
using Materia.Nodes.Attributes;

namespace Materia.Nodes.Atomic
{
    public class InvertNode : ImageNode
    {
        NodeInput input;
        NodeOutput output;

        bool red;
        bool green;
        bool blue;
        bool alpha;

        InvertProcessor processor;

        [Promote(NodeType.Bool)]
        [Editable(ParameterInputType.Toggle, "Red")]
        public bool Red
        {
            get
            {
                return red;
            }
            set
            {
                red = value;
                TryAndProcess();
            }
        }

        [Promote(NodeType.Bool)]
        [Editable(ParameterInputType.Toggle, "Green")]
        public bool Green
        {
            get
            {
                return green;
            }
            set
            {
                green = value;
                TryAndProcess();
            }
        }

        [Promote(NodeType.Bool)]
        [Editable(ParameterInputType.Toggle, "Blue")]
        public bool Blue
        {
            get
            {
                return blue;
            }
            set
            {
                blue = value;
                TryAndProcess();
            }
        }

        [Promote(NodeType.Bool)]
        [Editable(ParameterInputType.Toggle, "Alpha")]
        public bool Alpha
        {
            get
            {
                return alpha;
            }
            set
            {
                alpha = value;
                TryAndProcess();
            }
        }

        public InvertNode(int w, int h, GraphPixelType p = GraphPixelType.RGBA)
        {
            Name = "Invert";

            Id = Guid.NewGuid().ToString();

            width = w;
            height = h;
            red = true;
            blue = true;
            green = true;
            alpha = false;

            tileX = tileY = 1;

            previewProcessor = new BasicImageRenderer();
            processor = new InvertProcessor();

            internalPixelType = p;

            input = new NodeInput(NodeType.Color | NodeType.Gray, this, "Image Input");
            output = new NodeOutput(NodeType.Color | NodeType.Gray, this);

            input.OnInputAdded += Input_OnInputAdded;
            input.OnInputChanged += Input_OnInputChanged;
            input.OnInputRemoved += Input_OnInputRemoved;

            Inputs = new List<NodeInput>();
            Inputs.Add(input);

            Outputs = new List<NodeOutput>();
            Outputs.Add(output);
        }

        private void Input_OnInputRemoved(NodeInput n)
        {
            output.Data = null;
            output.Changed();
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
                if (input.HasInput)
                {
                    GetParams();
                    Process();
                }

                return;
            }

            if (input.HasInput)
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
                GetParams();
            })
            .ContinueWith(t =>
            {
                if(input.HasInput)
                {
                    Process();
                }
            }, Context);
        }

        private void GetParams()
        {
            pred = red;
            pgreen = green;
            pblue = blue;
            palpha = alpha;

            if(ParentGraph != null && ParentGraph.HasParameterValue(Id, "Red"))
            {
                pred = Convert.ToBoolean(ParentGraph.GetParameterValue(Id, "Red"));
            }

            if (ParentGraph != null && ParentGraph.HasParameterValue(Id, "Green"))
            {
                pgreen = Convert.ToBoolean(ParentGraph.GetParameterValue(Id, "Green"));
            }

            if (ParentGraph != null && ParentGraph.HasParameterValue(Id, "Blue"))
            {
                pblue = Convert.ToBoolean(ParentGraph.GetParameterValue(Id, "Blue"));
            }

            if (ParentGraph != null && ParentGraph.HasParameterValue(Id, "Alpha"))
            {
                palpha = Convert.ToBoolean(ParentGraph.GetParameterValue(Id, "Alpha"));
            }
        }

        bool pred;
        bool pgreen;
        bool pblue;
        bool palpha;
        void Process()
        {
            GLTextuer2D i1 = (GLTextuer2D)input.Input.Data;

            if (i1 == null) return;
            if (i1.Id == 0) return;

            CreateBufferIfNeeded();

            processor.TileX = tileX;
            processor.TileY = tileY;
            processor.Red = pred;
            processor.Green = pgreen;
            processor.Blue = pblue;
            processor.Alpha = palpha;

            processor.Process(width, height, i1, buffer);
            processor.Complete();

            Updated();
            output.Data = buffer;
            output.Changed();
        }

        public override void Dispose()
        {
            base.Dispose();
            
            if(processor != null)
            {
                processor.Release();
                processor = null;
            }
        }

        public class InvertNodeData : NodeData
        {
            public bool red;
            public bool blue;
            public bool green;
            public bool alpha;
        }

        public override void FromJson(string data)
        {
            InvertNodeData d = JsonConvert.DeserializeObject<InvertNodeData>(data);
            SetBaseNodeDate(d);

            red = d.red;
            green = d.green;
            blue = d.blue;
            alpha = d.alpha;
        }

        public override string GetJson()
        {
            InvertNodeData d = new InvertNodeData();
            FillBaseNodeData(d);
            d.red = red;
            d.green = green;
            d.blue = blue;
            d.alpha = alpha;

            return JsonConvert.SerializeObject(d);
        }

        protected override void OnWidthHeightSet()
        {
            TryAndProcess();
        }
    }
}
