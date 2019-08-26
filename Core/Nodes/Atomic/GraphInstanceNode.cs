﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using Materia.Nodes.Attributes;
using Materia.Textures;
using System.Threading;
using NLog;
using Materia.Archive;

namespace Materia.Nodes.Atomic
{
    public class GraphInstanceNode : ImageNode
    {
        private static ILogger Log = LogManager.GetCurrentClassLogger();

        public Graph GraphInst { get; protected set; }

        protected string path;
        protected Dictionary<string, object> jsonParameters;
        protected Dictionary<string, object> jsonCustomParameters;
        protected Dictionary<string, GraphParameterValue> nameMap;
        protected int randomSeed;
        protected bool updatingParams;

        protected bool loading;
        private bool isArchive;

        private MTGArchive archive;
        private MTGArchive child;

        //a shortcut reference
        private Graph topGraph;

        [Editable(ParameterInputType.GraphFile, "Materia Graph File", "Content")]
        public string GraphFilePath
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
                Load(path);
                Updated();
            }
        }

        [Editable(ParameterInputType.Map, "Parameters", "Instance Parameters")]
        public Dictionary<string, GraphParameterValue> Parameters
        {
            get
            {
                if(GraphInst != null)
                {
                    return GraphInst.Parameters;
                }

                return null;
            }
        }

        [Editable(ParameterInputType.Map, "Custom Parameters", "Instance Parameters")]
        public List<GraphParameterValue> CustomParameters
        {
            get
            {
                if(GraphInst != null)
                {
                    return GraphInst.CustomParameters;
                }

                return null;
            }
        }

        public int RandomSeed
        {
            get
            {
                if(GraphInst != null)
                {
                    return GraphInst.RandomSeed;
                }

                return 0;
            }
            set
            {
                GraphInst.RandomSeed = value;
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

        protected string GraphData { get; set; }

        public GraphInstanceNode(int w, int h, GraphPixelType p = GraphPixelType.RGBA)
        {
            width = w;
            height = h;

            nameMap = new Dictionary<string, GraphParameterValue>();

            Id = Guid.NewGuid().ToString();

            tileX = tileY = 1;

            internalPixelType = p;

            Name = "Graph Instance";

            this.path = "";

            //we do not initialize the inputs and outputs here
            //instead they are loaded after the graph is loaded
            Inputs = new List<NodeInput>();
            Outputs = new List<NodeOutput>();
        }

        public GraphParameterValue GetCustomParameter(string name)
        {
            GraphParameterValue v = null;
            nameMap.TryGetValue(name, out v);
            return v;
        }

        private void GraphParameterValue_OnGraphParameterUpdate(GraphParameterValue param)
        {
            if(GraphInst != null && !updatingParams && param.ParentGraph == GraphInst)
            {
                TryAndProcess();
            }
        }

        public override void TryAndProcess()
        {
            if (!Async)
            {
                PrepareProcess();
                if (GraphInst != null)
                {
                    GraphInst.TryAndProcess();
                }
                TryAndReleaseBuffers();
                return;
            }

            if (ParentGraph != null)
            {
                ParentGraph.Schedule(this);
            }
        }

        private void PrepareProcess()
        {
            if (GraphInst != null)
            {
                //handle assignment of upper parameter reassignment
                Graph p = ParentGraph;
                updatingParams = true;
                if (p != null)
                {
                    foreach (var k in Parameters.Keys)
                    {
                        if (Parameters[k].IsFunction()) continue;

                        string[] split = k.Split('.');

                        if (p.HasParameterValue(split[0], split[1]))
                        {
                            var realParam = Parameters[k];
                            realParam.AssignValue(p.GetParameterValue(split[0], split[1]));
                        }
                    }

                    int count = CustomParameters.Count;
                    for(int i = 0; i < count; i++)
                    {
                        var param = CustomParameters[i];
                        if (p.HasParameterValue(Id, param.Name))
                        {
                            param.AssignValue(p.GetParameterValue(Id, param.Name));
                        }
                    }
                }
                updatingParams = false;
            }
        }

        public override Task GetTask()
        {
            return Task.Factory.StartNew(() =>
            {
                PrepareProcess();
            })
            .ContinueWith(t =>
            {
                if (GraphInst != null)
                {
                    GraphInst.TryAndProcess();
                }
            }, Context);
        }

        public bool Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            isArchive = path.ToLower().EndsWith(".mtga");

            //convert path to a relative resource path
            string relative = Path.Combine("resources", Path.GetFileName(path));

            if (GraphInst != null)
            {
                GraphData = null;
                GraphInst.OnGraphParameterUpdate -= GraphParameterValue_OnGraphParameterUpdate;
                GraphInst.Dispose();
                GraphInst = null;
            }

            nameMap = new Dictionary<string, GraphParameterValue>();

            //handle archives within archives
            if(isArchive && archive != null)
            {
                archive.Open();
                var files = archive.GetAvailableFiles();
                var m = files.Find(f => f.path.Equals(relative));
                if(m != null)
                {
                    loading = true;
                    child = new MTGArchive(relative, m.ExtractBinary());
                    child.Open();
                    var childFiles = child.GetAvailableFiles();
                    if (childFiles != null)
                    {
                        var mtg = childFiles.Find(f => f.path.ToLower().EndsWith(".mtg"));
                        if (mtg != null)
                        {
                            loading = true;
                            this.path = path;
                            string nm = Path.GetFileNameWithoutExtension(path);
                            Name = nm;
                            GraphData = mtg.ExtractText();
                            child.Close();
                            PrepareGraph();
                            archive.Close();
                            loading = false;
                            return true;
                        } 
                        else
                        {
                            this.path = null;
                        }
                    }
                    else
                    {
                        this.path = null;
                    }
                }
                else
                {
                    this.path = null;
                }

                archive.Close();
            }
            //handle absolute path to archive when not in another archive
            else if(File.Exists(path) && isArchive && archive == null)
            {
                loading = true;
                child = new MTGArchive(path);
                child.Open();
                var childFiles = child.GetAvailableFiles();
                if (childFiles != null)
                {
                    var mtg = childFiles.Find(f => f.path.ToLower().EndsWith(".mtg"));
                    if (mtg != null)
                    {
                        loading = true;
                        this.path = path;
                        string nm = Path.GetFileNameWithoutExtension(path);
                        Name = nm;
                        GraphData = mtg.ExtractText();
                        child.Close();
                        PrepareGraph();
                        loading = false;
                        return true;
                    }
                    else
                    {
                        this.path = null;
                    }
                }
                else
                {
                    this.path = null;
                }
            }
            //otherwise try relative storage for the archive when not in another archive
            else if(isArchive && archive == null && ParentGraph != null && !string.IsNullOrEmpty(ParentGraph.CWD) && File.Exists(Path.Combine(ParentGraph.CWD, relative)))
            {
                string realPath = Path.Combine(ParentGraph.CWD, relative);
                child = new MTGArchive(realPath);
                child.Open();
                var childFiles = child.GetAvailableFiles();
                if (childFiles != null)
                {
                    var mtg = childFiles.Find(f => f.path.ToLower().EndsWith(".mtg"));
                    if (mtg != null)
                    {
                        loading = true;
                        this.path = path;
                        string nm = Path.GetFileNameWithoutExtension(path);
                        Name = nm;
                        GraphData = mtg.ExtractText();
                        child.Close();
                        PrepareGraph();
                        loading = false;
                        return true;
                    }
                    else
                    {
                        this.path = null;
                    }
                }
                else
                {
                    this.path = null;
                }
            }
            else if (!isArchive && File.Exists(path) && Path.GetExtension(path).ToLower().EndsWith("mtg"))
            {
                loading = true;
                this.path = path;
                string nm = Path.GetFileNameWithoutExtension(path);
                Name = nm;
                GraphData = File.ReadAllText(path);
                PrepareGraph();
                loading = false;
                return true;
            }
            else
            {
                this.path = null;
            }

            return false;
        }

        void PrepareGraph()
        {
            //the width and height here don't matter
            GraphInst = new Graph(Name, 256, 256, true, topGraph);
            GraphInst.AssignParentNode(this);
            GraphInst.Synchronized = !Async;
            GraphInst.FromJson(GraphData, child);
            GraphInst.AssignParameters(jsonParameters);
            GraphInst.AssignCustomParameters(jsonCustomParameters);
            GraphInst.AssignSeed(randomSeed);
            GraphInst.AssignPixelType(internalPixelType);
            //now do real initial resize
            GraphInst.ResizeWith(width, height);

            //mark as readonly
            GraphInst.ReadOnly = true;

            GraphInst.OnGraphParameterUpdate += GraphParameterValue_OnGraphParameterUpdate;

            //setup inputs and outputs
            Setup();
            loading = false;
        }

        void Setup()
        {
            int count = 0;
            if(GraphInst.InputNodes.Count > 0)
            {
                count = GraphInst.InputNodes.Count;
                for(int i = 0; i < count; i++)
                {
                    string id = GraphInst.InputNodes[i];
                    Node n;
                    if (GraphInst.NodeLookup.TryGetValue(id, out n))
                    {
                        InputNode inp = (InputNode)n;
                        NodeInput np = new NodeInput(NodeType.Color | NodeType.Gray, this, inp.Name);

                        inp.SetInput(np);
                        Inputs.Add(np);
                    }
                }
            }

            if(GraphInst.OutputNodes.Count > 0)
            {
                count = GraphInst.OutputNodes.Count;
                for(int i = 0; i < count; i++)
                {
                    string id = GraphInst.OutputNodes[i];
                    Node n;
                    if (GraphInst.NodeLookup.TryGetValue(id, out n))
                    {
                        OutputNode op = (OutputNode)n;

                        NodeOutput ot;

                        ot = new NodeOutput(NodeType.Color | NodeType.Gray, n, op.Name);
                        //we add to our graph instance outputs so things can actually connect 
                        //to the output
                        Outputs.Add(ot);
                        op.SetOutput(ot);

                        n.OnUpdate += N_OnUpdate;
                    }
                }
            }

            //name map used in parameter mapping for quicker lookup
            count = GraphInst.CustomParameters.Count;
            for(int i = 0; i < count; i++)
            {
                var param = GraphInst.CustomParameters[i];
                nameMap[param.Name] = param;
            }
        }

        private void N_OnUpdate(Node n)
        {
            Updated();
        }

        void TryAndReleaseBuffers()
        {
            if (GraphInst != null)
            {
                GraphInst.ReleaseIntermediateBuffers();
            }
        }

        public override byte[] GetPreview(int width, int height)
        {
            //we only show the first output as preview
            if(Outputs.Count > 0)
            {
                return Outputs[0].Node.GetPreview(width, height);
            }

            return null;
        }

        public override GLTextuer2D GetActiveBuffer()
        {
            if(Outputs.Count > 0)
            {
                return Outputs[0].Node.GetActiveBuffer();
            }

            return null;
        }

        public override void AssignParentGraph(Graph g)
        {
            base.AssignParentGraph(g);

            if (g != null)
            {
                topGraph = g.TopGraph();
            }
            else
            {
                topGraph = null;
            }
        }

        protected override void OnPixelFormatChange()
        {
            if (GraphInst != null)
            {
                GraphInst.AssignPixelType(internalPixelType);
            }

            base.OnPixelFormatChange();
        }

        public override void AssignPixelType(GraphPixelType pix)
        {
            base.AssignPixelType(pix);

            if(GraphInst != null)
            {
                GraphInst.AssignPixelType(pix);
            }
        }

        //we actually store the graph raw data
        //so this file can be transported without needing
        //the original graph file
        public class GraphInstanceNodeData : NodeData
        {
            public List<string> inputIds;
            public Dictionary<string, object> parameters;
            public Dictionary<string, object> customParameters;
            public int randomSeed;
            public string rawData;
            public string path;
        }

        public override void CopyResources(string CWD)
        {
            if(isArchive && archive == null && !string.IsNullOrEmpty(path))
            {
                string relative = Path.Combine("resources", Path.GetFileName(path));
                CopyResourceTo(CWD, relative, path);
            }
        }

        public override void FromJson(string data, MTGArchive arch = null)
        {
            archive = arch;
            FromJson(data);
        }

        //helper function for older graphs
        protected void ValidatePixelType()
        {
            if(internalPixelType != GraphPixelType.Luminance32F 
                && internalPixelType != GraphPixelType.Luminance16F
                && internalPixelType != GraphPixelType.RGB
                && internalPixelType != GraphPixelType.RGB16F
                && internalPixelType != GraphPixelType.RGB32F
                && internalPixelType != GraphPixelType.RGBA
                && internalPixelType != GraphPixelType.RGBA16F
                && internalPixelType != GraphPixelType.RGBA32F)
            {
                internalPixelType = GraphPixelType.RGBA;
            }
        }

        public override void FromJson(string data)
        {
            GraphInstanceNodeData d = JsonConvert.DeserializeObject<GraphInstanceNodeData>(data);
            SetBaseNodeDate(d);
            GraphData = d.rawData;
            path = d.path;
            jsonParameters = d.parameters;
            jsonCustomParameters = d.customParameters;
            randomSeed = d.randomSeed;

            ValidatePixelType();

            bool didLoad = false;

            //we do this incase 
            //the original graph was updated
            //and thus we should pull it in
            //if it exists
            //otherwise we fall back on
            //last saved graph data
            //also we do this
            //to try and load from 
            //archive first
            didLoad = Load(path);

            //if path not found or could not load
            //fall back to last instance data saved
            if (!didLoad)
            {
                nameMap = new Dictionary<string, GraphParameterValue>();
                loading = true;
                GraphInst = new Graph(Name, 256, 256, true, topGraph);
                GraphInst.AssignParentNode(this);
                GraphInst.Synchronized = !Async;
                GraphInst.FromJson(GraphData);
                GraphInst.AssignParameters(jsonParameters);
                GraphInst.AssignCustomParameters(jsonCustomParameters);
                GraphInst.AssignSeed(randomSeed);
                GraphInst.AssignPixelType(internalPixelType);
                GraphInst.ResizeWith(width, height);
                GraphInst.OnGraphParameterUpdate += GraphParameterValue_OnGraphParameterUpdate;

                Setup();
                loading = false;
            }
        }

        public override string GetJson()
        {
            GraphInstanceNodeData d = new GraphInstanceNodeData();
            FillBaseNodeData(d);
            d.rawData = GraphData;
            d.path = path;
            d.parameters = GraphInst.GetConstantParameters();
            d.customParameters = GraphInst.GetCustomParameters();
            d.randomSeed = RandomSeed;

            return JsonConvert.SerializeObject(d);
        }

        protected override void OnWidthHeightSet()
        {
            if(GraphInst != null)
            {
                GraphInst.ResizeWith(width, height);
                GraphInst.TryAndProcess();
            }

            Updated();
        }

        public override void Dispose()
        {
            if(child != null)
            {
                child.Dispose();
                child = null;
            }

            base.Dispose();

            if(GraphInst != null)
            {
                GraphInst.OnGraphParameterUpdate -= GraphParameterValue_OnGraphParameterUpdate;
                GraphInst.Dispose();
                GraphInst = null;
            }
        }
    }
}
