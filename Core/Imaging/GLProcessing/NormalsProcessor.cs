﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Materia.Shaders;
using Materia.Textures;
using Materia.Math3D;
using Materia.GLInterfaces;

namespace Materia.Imaging.GLProcessing
{
    public class NormalsProcessor : ImageProcessor
    {
        public float Intensity { get; set; }
        public bool DirectX { get; set; }
        public float NoiseReduction { get; set; }

        IGLProgram shader;

        public NormalsProcessor() : base()
        {
            shader = GetShader("image.glsl", "normals.glsl");
        }

        public override void Process(int width, int height, GLTextuer2D tex, GLTextuer2D output)
        {
            base.Process(width, height, tex, output);

            if (shader != null)
            {
                ResizeViewTo(tex, output, tex.Width, tex.Height, width, height);
                tex = output;
                IGL.Primary.Clear((int)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

                Vector2 tiling = new Vector2(TileX, TileY);

                shader.Use();
                shader.SetUniform2("tiling", ref tiling);
                shader.SetUniform("MainTex", 0);
                shader.SetUniform("directx", DirectX);
                shader.SetUniform("intensity", Intensity);
                shader.SetUniform("width", (float)width);
                shader.SetUniform("height", (float)height);
                shader.SetUniform("reduce", NoiseReduction);
                IGL.Primary.ActiveTexture((int)TextureUnit.Texture0);
                tex.Bind();

                if (renderQuad != null)
                {
                    renderQuad.Draw();
                }

                GLTextuer2D.Unbind();
                //output.Bind();
                //output.CopyFromFrameBuffer(width, height);
                //GLTextuer2D.Unbind();
                Blit(output, width, height);
            }
        }
    }
}
