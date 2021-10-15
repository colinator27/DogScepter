using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker shader.
    /// </summary>
    public class GMShader : GMSerializable
    {
        public GMString Name;
        public ShaderType Type;
        public GMString GLSL_ES_Vertex;
        public GMString GLSL_ES_Fragment;
        public GMString GLSL_Vertex;
        public GMString GLSL_Fragment;
        public GMString HLSL9_Vertex;
        public GMString HLSL9_Fragment;

        public ShaderBuffer HLSL11_VertexBuffer;
        public ShaderBuffer HLSL11_PixelBuffer;
        public ShaderBuffer PSSL_VertexBuffer;
        public ShaderBuffer PSSL_PixelBuffer;
        public ShaderBuffer CG_PSV_VertexBuffer;
        public ShaderBuffer CG_PSV_PixelBuffer;
        public ShaderBuffer CG_PS3_VertexBuffer;
        public ShaderBuffer CG_PS3_PixelBuffer;

        public List<GMString> VertexAttributes;

        public int Version = 2;

        public enum ShaderType : int
        {
            GLSL_ES = 1,
            GLSL = 2,
            HLSL9 = 3,
            HLSL11 = 4,
            PSSL = 5,
            Cg_PSVita = 6,
            Cg_PS3 = 7
        }

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.Write((uint)Type | 0x80000000u);

            writer.WritePointerString(GLSL_ES_Vertex);
            writer.WritePointerString(GLSL_ES_Fragment);
            writer.WritePointerString(GLSL_Vertex);
            writer.WritePointerString(GLSL_Fragment);
            writer.WritePointerString(HLSL9_Vertex);
            writer.WritePointerString(HLSL9_Fragment);

            writer.WritePointer(HLSL11_VertexBuffer);
            writer.WritePointer(HLSL11_PixelBuffer);

            writer.Write(VertexAttributes.Count);
            foreach (GMString s in VertexAttributes)
                writer.WritePointerString(s);

            writer.Write(Version);

            writer.WritePointer(PSSL_VertexBuffer);
            writer.Write((PSSL_VertexBuffer != null) ? PSSL_VertexBuffer.Buffer.Length : 0);
            writer.WritePointer(PSSL_PixelBuffer);
            writer.Write((PSSL_PixelBuffer != null) ? PSSL_PixelBuffer.Buffer.Length : 0);

            writer.WritePointer(CG_PSV_VertexBuffer);
            writer.Write((CG_PSV_VertexBuffer != null) ? CG_PSV_VertexBuffer.Buffer.Length : 0);
            writer.WritePointer(CG_PSV_PixelBuffer);
            writer.Write((CG_PSV_PixelBuffer != null) ? CG_PSV_PixelBuffer.Buffer.Length : 0);

            if (Version >= 2)
            {
                writer.WritePointer(CG_PS3_VertexBuffer);
                writer.Write((CG_PS3_VertexBuffer != null) ? CG_PS3_VertexBuffer.Buffer.Length : 0);
                writer.WritePointer(CG_PS3_PixelBuffer);
                writer.Write((CG_PS3_PixelBuffer != null) ? CG_PS3_PixelBuffer.Buffer.Length : 0);
            }

            if (HLSL11_VertexBuffer != null)
            {
                writer.Pad(8);
                writer.WriteObjectPointer(HLSL11_VertexBuffer);
                HLSL11_VertexBuffer.Serialize(writer);
            }
            if (HLSL11_PixelBuffer != null)
            {
                writer.Pad(8);
                writer.WriteObjectPointer(HLSL11_PixelBuffer);
                HLSL11_PixelBuffer.Serialize(writer);
            }

            if (PSSL_VertexBuffer != null)
            {
                writer.Pad(8);
                writer.WriteObjectPointer(PSSL_VertexBuffer);
                PSSL_VertexBuffer.Serialize(writer);
            }
            if (PSSL_PixelBuffer != null)
            {
                writer.Pad(8);
                writer.WriteObjectPointer(PSSL_PixelBuffer);
                PSSL_PixelBuffer.Serialize(writer);
            }

            if (CG_PSV_VertexBuffer != null)
            {
                writer.Pad(8);
                writer.WriteObjectPointer(CG_PSV_VertexBuffer);
                CG_PSV_VertexBuffer.Serialize(writer);
            }
            if (CG_PSV_PixelBuffer != null)
            {
                writer.Pad(8);
                writer.WriteObjectPointer(CG_PSV_PixelBuffer);
                CG_PSV_PixelBuffer.Serialize(writer);
            }

            if (Version >= 2)
            {
                if (CG_PS3_VertexBuffer != null)
                {
                    writer.Pad(16);
                    writer.WriteObjectPointer(CG_PS3_VertexBuffer);
                    CG_PS3_VertexBuffer.Serialize(writer);
                }
                if (CG_PS3_PixelBuffer != null)
                {
                    writer.Pad(8);
                    writer.WriteObjectPointer(CG_PS3_PixelBuffer);
                    CG_PS3_PixelBuffer.Serialize(writer);
                }
            }
        }

        public void Unserialize(GMDataReader reader)
        {
            throw new NotImplementedException();
        }

        public void Unserialize(GMDataReader reader, int endPos)
        {
            Name = reader.ReadStringPointerObject();
            Type = (ShaderType)(reader.ReadUInt32() & 0x7FFFFFFF);

            GLSL_ES_Vertex = reader.ReadStringPointerObject();
            GLSL_ES_Fragment = reader.ReadStringPointerObject();
            GLSL_Vertex = reader.ReadStringPointerObject();
            GLSL_Fragment = reader.ReadStringPointerObject();
            HLSL9_Vertex = reader.ReadStringPointerObject();
            HLSL9_Fragment = reader.ReadStringPointerObject();

            int ptr1 = reader.ReadInt32();
            HLSL11_VertexBuffer = reader.ReadPointer<ShaderBuffer>(ptr1);
            int ptr2 = reader.ReadInt32();
            HLSL11_PixelBuffer = reader.ReadPointer<ShaderBuffer>(ptr2);

            int count = reader.ReadInt32();
            VertexAttributes = new List<GMString>(count);
            for (int i = count; i > 0; i--)
                VertexAttributes.Add(reader.ReadStringPointerObject());

            Version = reader.ReadInt32();

            int ptr3 = reader.ReadInt32();
            PSSL_VertexBuffer = reader.ReadPointer<ShaderBuffer>(ptr3);
            ReadShaderData(reader, PSSL_VertexBuffer, ptr3, reader.ReadInt32());

            int currPtr = reader.ReadInt32();
            PSSL_PixelBuffer = reader.ReadPointer<ShaderBuffer>(currPtr);
            ReadShaderData(reader, PSSL_PixelBuffer, currPtr, reader.ReadInt32());

            currPtr = reader.ReadInt32();
            CG_PSV_VertexBuffer = reader.ReadPointer<ShaderBuffer>(currPtr);
            ReadShaderData(reader, CG_PSV_VertexBuffer, currPtr, reader.ReadInt32());

            currPtr = reader.ReadInt32();
            CG_PSV_PixelBuffer = reader.ReadPointer<ShaderBuffer>(currPtr);
            ReadShaderData(reader, CG_PSV_PixelBuffer, currPtr, reader.ReadInt32());

            if (Version >= 2)
            {
                currPtr = reader.ReadInt32();
                CG_PS3_VertexBuffer = reader.ReadPointer<ShaderBuffer>(currPtr);
                ReadShaderData(reader, CG_PS3_VertexBuffer, currPtr, reader.ReadInt32());

                currPtr = reader.ReadInt32();
                CG_PS3_PixelBuffer = reader.ReadPointer<ShaderBuffer>(currPtr);
                ReadShaderData(reader, CG_PS3_PixelBuffer, currPtr, reader.ReadInt32());
            }

            ReadShaderData(reader, HLSL11_VertexBuffer, ptr1, -1, ptr2 == 0 ? endPos : ptr2);
            ReadShaderData(reader, HLSL11_PixelBuffer, ptr2, -1, ptr3 == 0 ? endPos : ptr3);
        }

        private void ReadShaderData(GMDataReader reader, ShaderBuffer buf, int ptr, int length = -1, int end = -1)
        {
            if (buf == null)
                return;

            int returnTo = reader.Offset;
            reader.Offset = ptr;

            if (length == -1)
                buf.Unserialize(reader, end - ptr);
            else
                buf.Unserialize(reader, length);

            reader.Offset = returnTo;
        }

        public override string ToString()
        {
            return $"Shader: \"{Name.Content}\"";
        }

        /// <summary>
        /// Contains compiled shader data.
        /// </summary>
        public class ShaderBuffer : GMSerializable
        {
            public BufferRegion Buffer;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(Buffer);
            }

            public void Unserialize(GMDataReader reader)
            {
                throw new NotImplementedException();
            }

            public void Unserialize(GMDataReader reader, int length)
            {
                Buffer = reader.ReadBytes(length);
            }
        }
    }
}
