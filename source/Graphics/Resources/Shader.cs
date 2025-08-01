﻿using System;
using System.Runtime.InteropServices;
using Nano.Storage;
using SDL = Nano.Graphics.SDL_GPU;

namespace Nano.Graphics
{
	/// <summary>
	/// Shaders are used to create graphics pipelines.
	/// Graphics pipelines take a vertex shader and a fragment shader.
	/// </summary>
	public class Shader : SDLGPUResource
	{
		protected override Action<IntPtr, IntPtr> ReleaseFunction => SDL.SDL_ReleaseGPUShader;

		public uint NumSamplers { get; init; }
		public uint NumStorageTextures { get; init; }
		public uint NumStorageBuffers { get; init; }
		public uint NumUniformBuffers { get; init; }

		// Optional, filled in by ShaderCross or JSON loading
		public IOVarMetadata[] Inputs { get; init; }
		public IOVarMetadata[] Outputs { get; init; }

		private Shader(GraphicsDevice device) : base(device)
		{
			Name = "Shader";
		}

		/// <summary>
		/// Creates a shader using a specified shader format.
		/// </summary>
		public static unsafe Shader Create(
			GraphicsDevice device,
			TitleStorage storage,
			string filePath,
			string entryPoint,
			in ShaderCreateInfo shaderCreateInfo
		) {
			if (!storage.GetFileSize(filePath, out var size))
			{
				return null;
			}

			var buffer = NativeMemory.Alloc((nuint) size);
			var span = new Span<byte>(buffer, (int) size);
			if (!storage.ReadFile(filePath, span))
			{
				return null;
			}

			var pipeline = Create(device, span, entryPoint, shaderCreateInfo);
			NativeMemory.Free(buffer);
			return pipeline;
		}

		/// <summary>
		/// Creates a shader using a specified shader format.
		/// </summary>
		public static unsafe Shader Create(
			GraphicsDevice device,
			ReadOnlySpan<byte> span,
			string entryPoint,
			in ShaderCreateInfo shaderCreateInfo
		) {
			var entryPointBuffer = InteropUtilities.EncodeToUTF8Buffer(entryPoint);

			fixed (byte* spanPtr = span)
			{
				INTERNAL_ShaderCreateInfo createInfo;
				createInfo.CodeSize = (nuint) span.Length;
				createInfo.Code = spanPtr;
				createInfo.EntryPoint = entryPointBuffer;
				createInfo.Stage = shaderCreateInfo.Stage;
				createInfo.Format = shaderCreateInfo.Format;
				createInfo.NumSamplers = shaderCreateInfo.NumSamplers;
				createInfo.NumStorageTextures = shaderCreateInfo.NumStorageTextures;
				createInfo.NumStorageBuffers = shaderCreateInfo.NumStorageBuffers;
				createInfo.NumUniformBuffers = shaderCreateInfo.NumUniformBuffers;
				createInfo.Props = shaderCreateInfo.Props;

				var cleanProps = false;
				if (shaderCreateInfo.Name != null)
				{
					if (createInfo.Props == 0)
					{
						createInfo.Props = SDL3.SDL.SDL_CreateProperties();
						cleanProps = true;
					}

					SDL3.SDL.SDL_SetStringProperty(createInfo.Props, SDL3.SDL.SDL_PROP_GPU_SHADER_CREATE_NAME_STRING, shaderCreateInfo.Name);
				}

				var shaderModule = SDL.SDL_CreateGPUShader(
					device.Handle,
					createInfo
				);

				NativeMemory.Free(entryPointBuffer);

				if (shaderModule == nint.Zero)
				{
					Logger.LogError("Failed to compile shader!");
					Logger.LogError(SDL3.SDL.SDL_GetError());
					return null;
				}

				var shader = new Shader(device)
				{
					Handle = shaderModule,
					NumSamplers = shaderCreateInfo.NumSamplers,
					NumStorageTextures = shaderCreateInfo.NumStorageTextures,
					NumStorageBuffers = shaderCreateInfo.NumStorageBuffers,
					NumUniformBuffers = shaderCreateInfo.NumUniformBuffers
				};

				if (cleanProps)
				{
					SDL3.SDL.SDL_DestroyProperties(createInfo.Props);
				}

				return shader;
			}
		}

		/// <summary>
		/// Creates a shader for any backend from SPIRV bytecode.
		/// </summary>
		internal static unsafe Shader CreateFromSPIRV(
			GraphicsDevice device,
			string name, // can be null
			ReadOnlySpan<byte> span,
			string entryPoint,
			ShaderStage shaderStage,
			bool enableDebug
		) {
			var entryPointBuffer = InteropUtilities.EncodeToUTF8Buffer(entryPoint);
			var nameBuffer = InteropUtilities.EncodeToUTF8Buffer(name);

			fixed (byte* spanPtr = span)
			{
				SDL_ShaderCross.INTERNAL_GraphicsShaderMetadata* metadata = SDL_ShaderCross.SDL_ShaderCross_ReflectGraphicsSPIRV(
					(nint) spanPtr,
					(nuint) span.Length,
					0
				);

				SDL_ShaderCross.INTERNAL_SPIRVInfo spirvInfo;
				spirvInfo.Bytecode = spanPtr;
				spirvInfo.BytecodeSize = (nuint) span.Length;
				spirvInfo.EntryPoint = entryPointBuffer;
				spirvInfo.ShaderStage = (SDL_ShaderCross.ShaderStage) shaderStage;
				spirvInfo.EnableDebug = enableDebug;
				spirvInfo.Name = nameBuffer;
				spirvInfo.Props = 0;

				var shaderModule = SDL_ShaderCross.SDL_ShaderCross_CompileGraphicsShaderFromSPIRV(
					device.Handle,
					spirvInfo,
					metadata,
					0
				);

				NativeMemory.Free(entryPointBuffer);
				NativeMemory.Free(nameBuffer);

				if (shaderModule == nint.Zero)
				{
					Logger.LogError("Failed to compile shader!");
					Logger.LogError(SDL3.SDL.SDL_GetError());
					return null;
				}

				var inputs = new IOVarMetadata[metadata->NumInputs];
				for (var i = 0; i < metadata->NumInputs; i += 1)
				{
					inputs[i] = new IOVarMetadata
					{
						Name = InteropUtilities.DecodeFromCString(metadata->Inputs[i].Name, 64),
						Location = metadata->Inputs[i].Location,
						Offset = metadata->Inputs[i].Offset,
						VectorType = (IOVarType) metadata->Inputs[i].VectorType,
						VectorSize = metadata->Inputs[i].VectorSize
					};
				}

				var outputs = new IOVarMetadata[metadata->NumOutputs];
				for (var i = 0; i < metadata->NumOutputs; i += 1)
				{
					outputs[i] = new IOVarMetadata
					{
						Name = InteropUtilities.DecodeFromCString(metadata->Outputs[i].Name, 64),
						Location = metadata->Outputs[i].Location,
						Offset = metadata->Outputs[i].Offset,
						VectorType = (IOVarType) metadata->Outputs[i].VectorType,
						VectorSize = metadata->Outputs[i].VectorSize
					};
				}

				var shader = new Shader(device)
				{
					Handle = shaderModule,
					NumSamplers = metadata->NumSamplers,
					NumStorageTextures = metadata->NumStorageTextures,
					NumStorageBuffers = metadata->NumStorageBuffers,
					NumUniformBuffers = metadata->NumUniformBuffers,
					Inputs = inputs,
					Outputs = outputs,
					Name = name ?? "Shader"
				};

				SDL3.SDL.SDL_free((nint)metadata);

				return shader;
			}
		}

		/// <summary>
		/// Creates a shader for any backend from HLSL source.
		/// </summary>
		internal static unsafe Shader CreateFromHLSL(
			GraphicsDevice device,
			string name, // can be NULL
			ReadOnlySpan<byte> span,
			string entryPoint,
			string includeDir, // can be NULL
			ShaderStage shaderStage,
			bool enableDebug,
			params Span<ShaderCross.HLSLDefine> defines
		) {
			var entryPointBuffer = InteropUtilities.EncodeToUTF8Buffer(entryPoint);
			var includeDirBuffer = InteropUtilities.EncodeToUTF8Buffer(includeDir);
			var nameBuffer = InteropUtilities.EncodeToUTF8Buffer(name);

			fixed (byte* spanPtr = span)
			{
				SDL_ShaderCross.INTERNAL_HLSLDefine* definesBuffer = null;
				if (defines.Length > 0)
				{
					definesBuffer = (SDL_ShaderCross.INTERNAL_HLSLDefine*) NativeMemory.Alloc((nuint) (Marshal.SizeOf<SDL_ShaderCross.INTERNAL_HLSLDefine>() * (defines.Length + 1)));
					for (var i = 0; i < defines.Length; i += 1)
					{
						definesBuffer[i].Name = InteropUtilities.EncodeToUTF8Buffer(defines[i].Name);
						definesBuffer[i].Value = InteropUtilities.EncodeToUTF8Buffer(defines[i].Value);
					}
					// Null-terminate the array
					definesBuffer[defines.Length].Name = null;
					definesBuffer[defines.Length].Value = null;
				}

				SDL_ShaderCross.INTERNAL_HLSLInfo hlslInfo;
				hlslInfo.Source = spanPtr;
				hlslInfo.EntryPoint = entryPointBuffer;
				hlslInfo.IncludeDir = includeDirBuffer;
				hlslInfo.Defines = definesBuffer;
				hlslInfo.ShaderStage = (SDL_ShaderCross.ShaderStage) shaderStage;
				hlslInfo.EnableDebug = enableDebug;
				hlslInfo.Name = nameBuffer;
				hlslInfo.Props = 0;

				var spirvBytecode = SDL_ShaderCross.SDL_ShaderCross_CompileSPIRVFromHLSL(
					hlslInfo,
					out var spirvBytecodeSize
				);

				var metadata = SDL_ShaderCross.SDL_ShaderCross_ReflectGraphicsSPIRV(
					spirvBytecode,
					spirvBytecodeSize,
					0
				);

				SDL_ShaderCross.INTERNAL_SPIRVInfo spirvInfo;
				spirvInfo.Bytecode = (byte*) spirvBytecode;
				spirvInfo.BytecodeSize = spirvBytecodeSize;
				spirvInfo.EntryPoint = entryPointBuffer;
				spirvInfo.ShaderStage = (SDL_ShaderCross.ShaderStage) shaderStage;
				spirvInfo.EnableDebug = enableDebug;
				spirvInfo.Name = nameBuffer;
				spirvInfo.Props = 0;

				var shaderModule = SDL_ShaderCross.SDL_ShaderCross_CompileGraphicsShaderFromSPIRV(
					device.Handle,
					spirvInfo,
					metadata,
					0
				);

				SDL3.SDL.SDL_free(spirvBytecode);
				NativeMemory.Free(entryPointBuffer);
				NativeMemory.Free(includeDirBuffer);
				for (var i = 0; i < defines.Length; i += 1)
				{
					NativeMemory.Free(definesBuffer[i].Name);
					NativeMemory.Free(definesBuffer[i].Value);
				}
				NativeMemory.Free(definesBuffer);
				NativeMemory.Free(nameBuffer);

				if (shaderModule == nint.Zero)
				{
					Logger.LogError("Failed to compile shader!");
					Logger.LogError(SDL3.SDL.SDL_GetError());
					return null;
				}

				var inputs = new IOVarMetadata[metadata->NumInputs];
				for (var i = 0; i < metadata->NumInputs; i += 1)
				{
					inputs[i] = new IOVarMetadata
					{
						Name = InteropUtilities.DecodeFromCString(metadata->Inputs[i].Name, 64),
						Location = metadata->Inputs[i].Location,
						Offset = metadata->Inputs[i].Offset,
						VectorType = (IOVarType) metadata->Inputs[i].VectorType,
						VectorSize = metadata->Inputs[i].VectorSize
					};
				}

				var outputs = new IOVarMetadata[metadata->NumOutputs];
				for (var i = 0; i < metadata->NumOutputs; i += 1)
				{
					outputs[i] = new IOVarMetadata
					{
						Name = InteropUtilities.DecodeFromCString(metadata->Outputs[i].Name, 64),
						Location = metadata->Outputs[i].Location,
						Offset = metadata->Outputs[i].Offset,
						VectorType = (IOVarType) metadata->Outputs[i].VectorType,
						VectorSize = metadata->Outputs[i].VectorSize
					};
				}

				var shader = new Shader(device)
				{
					Handle = shaderModule,
					NumSamplers = metadata->NumSamplers,
					NumStorageTextures = metadata->NumStorageTextures,
					NumStorageBuffers = metadata->NumStorageBuffers,
					NumUniformBuffers = metadata->NumUniformBuffers,
					Inputs = inputs,
					Outputs = outputs,
					Name = name ?? "Shader"
				};

				SDL3.SDL.SDL_free((nint) metadata);

				return shader;
			}
		}
	}
}
