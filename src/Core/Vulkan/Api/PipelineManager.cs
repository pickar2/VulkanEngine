using System;
using System.Collections.Generic;
using System.Linq;
using Core.Native.Shaderc;
using Core.Native.SpirvReflect;
using Core.Vulkan.Utility;
using Silk.NET.Vulkan;
using SimpleMath.Vectors;

namespace Core.Vulkan.Api;

public static unsafe class PipelineManager
{
	public static readonly OnAccessValueReCreator<PipelineCache> PipelineCache;

	static PipelineManager() => PipelineCache = ReCreate.InDevice.OnAccessValue(() => CreatePipelineCache(), cache => cache.Dispose());

	private static PipelineCache CreatePipelineCache()
	{
		// TODO: load cache from disk
		var cacheCreateInfo = new PipelineCacheCreateInfo
		{
			SType = StructureType.PipelineCacheCreateInfo,
			InitialDataSize = 0
		};
		Context.Vk.CreatePipelineCache(Context.Device, &cacheCreateInfo, null, out var cache);

		return cache;
	}

	public static GraphicsPipelineBuilder GraphicsBuilder() => new();
}

public unsafe class GraphicsPipelineBuilder
{
	private PipelineCreateFlags _pipelineCreateFlags;

	private readonly Dictionary<ShaderKind, VulkanShader> _shaders = new();

	private PipelineInputAssemblyStateCreateInfo _inputAssemblyState;

	private Viewport _viewport;
	private Rect2D _scissor;
	private PipelineViewportStateCreateInfo _viewportState;

	private PipelineRasterizationStateCreateInfo _rasterizationState;

	private PipelineMultisampleStateCreateInfo _multisampleState;

	private PipelineDepthStencilStateCreateInfo _depthStencilState;

	private readonly List<PipelineColorBlendAttachmentState> _colorBlendAttachmentStates = new();
	private PipelineColorBlendStateCreateInfo _colorBlendState;

	private PipelineDynamicStateCreateInfo _dynamicState;

	public GraphicsPipelineBuilder() => ResetToDefault();

	public GraphicsPipelineBuilder WithShader(VulkanShader shader)
	{
		_shaders[shader.ShaderKind] = shader;

		return this;
	}

	public GraphicsPipelineBuilder ResetToDefault()
	{
		_pipelineCreateFlags = 0;

		_shaders.Clear();

		_inputAssemblyState = new PipelineInputAssemblyStateCreateInfo
		{
			SType = StructureType.PipelineInputAssemblyStateCreateInfo,
			Topology = PrimitiveTopology.TriangleList
		};

		SetViewportAndScissorFromSize(Context.State.WindowSize);

		_rasterizationState = new PipelineRasterizationStateCreateInfo
		{
			SType = StructureType.PipelineRasterizationStateCreateInfo,
			PolygonMode = PolygonMode.Fill,
			LineWidth = 1,
			CullMode = CullModeFlags.None,
			FrontFace = FrontFace.CounterClockwise
		};

		_multisampleState = new PipelineMultisampleStateCreateInfo
		{
			SType = StructureType.PipelineMultisampleStateCreateInfo,
			SampleShadingEnable = false,
			MinSampleShading = 0,
			RasterizationSamples = SampleCountFlags.Count1Bit
		};

		_depthStencilState = new PipelineDepthStencilStateCreateInfo
		{
			SType = StructureType.PipelineDepthStencilStateCreateInfo,
			DepthTestEnable = false
		};

		_colorBlendAttachmentStates.Clear();
		_colorBlendState = new PipelineColorBlendStateCreateInfo
		{
			SType = StructureType.PipelineColorBlendStateCreateInfo
		};

		_dynamicState = new PipelineDynamicStateCreateInfo
		{
			SType = StructureType.PipelineDynamicStateCreateInfo
		};

		return this;
	}

	public GraphicsPipelineBuilder SetDynamicState(PipelineDynamicStateCreateInfo dynamicState)
	{
		_dynamicState = dynamicState;
		return this;
	}

	public GraphicsPipelineBuilder DynamicState(Func<PipelineDynamicStateCreateInfo, PipelineDynamicStateCreateInfo> updater)
	{
		_dynamicState = updater.Invoke(_dynamicState);
		return this;
	}

	public GraphicsPipelineBuilder SetColorBlendState(PipelineColorBlendStateCreateInfo colorBlendState)
	{
		_colorBlendState = colorBlendState;
		return this;
	}

	public GraphicsPipelineBuilder ColorBlendState(Func<PipelineColorBlendStateCreateInfo, PipelineColorBlendStateCreateInfo> updater)
	{
		_colorBlendState = updater.Invoke(_colorBlendState);
		return this;
	}

	public GraphicsPipelineBuilder AddColorBlendAttachment(PipelineColorBlendAttachmentState attachmentState)
	{
		_colorBlendAttachmentStates.Add(attachmentState);
		return this;
	}

	public GraphicsPipelineBuilder AddColorBlendAttachmentOneMinusSrcAlpha()
	{
		var attachmentState = new PipelineColorBlendAttachmentState
		{
			BlendEnable = true,
			SrcColorBlendFactor = BlendFactor.SrcAlpha,
			DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
			ColorBlendOp = BlendOp.Add,
			SrcAlphaBlendFactor = BlendFactor.One,
			DstAlphaBlendFactor = BlendFactor.Zero,
			AlphaBlendOp = BlendOp.Add,
			ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
		};

		_colorBlendAttachmentStates.Add(attachmentState);
		return this;
	}

	public GraphicsPipelineBuilder AddColorBlendAttachmentBlendDisabled()
	{
		var attachmentState = new PipelineColorBlendAttachmentState
		{
			BlendEnable = false
		};

		_colorBlendAttachmentStates.Add(attachmentState);
		return this;
	}

	public GraphicsPipelineBuilder SetDepthStencilState(PipelineDepthStencilStateCreateInfo depthStencilState)
	{
		_depthStencilState = depthStencilState;
		return this;
	}

	public GraphicsPipelineBuilder DepthStencilState(Func<PipelineDepthStencilStateCreateInfo, PipelineDepthStencilStateCreateInfo> updater)
	{
		_depthStencilState = updater.Invoke(_depthStencilState);
		return this;
	}

	public GraphicsPipelineBuilder SetMultisampleState(PipelineMultisampleStateCreateInfo multisampleState)
	{
		_multisampleState = multisampleState;
		return this;
	}

	public GraphicsPipelineBuilder MultisampleState(Func<PipelineMultisampleStateCreateInfo, PipelineMultisampleStateCreateInfo> updater)
	{
		_multisampleState = updater.Invoke(_multisampleState);
		return this;
	}

	public GraphicsPipelineBuilder SetRasterizationState(PipelineRasterizationStateCreateInfo rasterizationState)
	{
		_rasterizationState = rasterizationState;
		return this;
	}

	public GraphicsPipelineBuilder RasterizationState(Func<PipelineRasterizationStateCreateInfo, PipelineRasterizationStateCreateInfo> updater)
	{
		_rasterizationState = updater.Invoke(_rasterizationState);
		return this;
	}

	public GraphicsPipelineBuilder SetViewport(Viewport viewport)
	{
		_viewport = viewport;
		return this;
	}

	public GraphicsPipelineBuilder Viewport(Func<Viewport, Viewport> updater)
	{
		_viewport = updater.Invoke(_viewport);
		return this;
	}

	public GraphicsPipelineBuilder SetScissor(Rect2D scissor)
	{
		_scissor = scissor;
		return this;
	}

	public GraphicsPipelineBuilder Scissor(Func<Rect2D, Rect2D> updater)
	{
		_scissor = updater.Invoke(_scissor);
		return this;
	}

	public GraphicsPipelineBuilder SetViewportState(PipelineViewportStateCreateInfo viewportState)
	{
		_viewportState = viewportState;
		return this;
	}

	public GraphicsPipelineBuilder ViewportState(Func<PipelineViewportStateCreateInfo, PipelineViewportStateCreateInfo> updater)
	{
		_viewportState = updater.Invoke(_viewportState);
		return this;
	}

	public GraphicsPipelineBuilder SetViewportAndScissorFromSize(Vector2<uint> size)
	{
		_viewport = new Viewport
		{
			X = 0, Y = 0,
			Width = size.X, Height = size.Y,
			MinDepth = 0, MaxDepth = 1
		};

		_scissor = new Rect2D(new Offset2D(0, 0), new Extent2D(size.X, size.Y));

		return this;
	}

	public GraphicsPipelineBuilder PipelineCreateFlags(Func<PipelineCreateFlags, PipelineCreateFlags> updater)
	{
		_pipelineCreateFlags = updater.Invoke(_pipelineCreateFlags);
		return this;
	}

	public GraphicsPipelineBuilder SetPipelineCreateFlags(PipelineCreateFlags flags)
	{
		_pipelineCreateFlags = flags;
		return this;
	}

	public Pipeline Build(PipelineLayout pipelineLayout, RenderPass renderPass, uint subpass = 0, Pipeline basePipeline = default, int basePipelineIndex = -1)
	{
		var shaderStagesArr = _shaders.Select(pair => pair.Value.ShaderCreateInfo()).ToArray();

		PipelineVertexInputStateCreateInfo vertexInputState = default;
		if (_shaders.TryGetValue(ShaderKind.VertexShader, out var vertexShader))
		{
			vertexInputState = ReflectUtils.VertexInputStateFromShader(vertexShader);
		}

		_viewportState = new PipelineViewportStateCreateInfo
		{
			SType = StructureType.PipelineViewportStateCreateInfo,
			ViewportCount = 1,
			PViewports = _viewport.AsPointer(),
			ScissorCount = 1,
			PScissors = _scissor.AsPointer()
		};

		var colorBlendAttachmentStatesArr = _colorBlendAttachmentStates.ToArray();
		_colorBlendState.AttachmentCount = (uint) colorBlendAttachmentStatesArr.Length;
		_colorBlendState.PAttachments = colorBlendAttachmentStatesArr[0].AsPointer();

		var createInfo = new GraphicsPipelineCreateInfo
		{
			SType = StructureType.GraphicsPipelineCreateInfo,
			Flags = _pipelineCreateFlags,
			StageCount = (uint) shaderStagesArr.Length,
			PStages = shaderStagesArr[0].AsPointer(),
			PVertexInputState = &vertexInputState,
			PInputAssemblyState = _inputAssemblyState.AsPointer(),
			PTessellationState = default,
			PViewportState = _viewportState.AsPointer(),
			PRasterizationState = _rasterizationState.AsPointer(),
			PMultisampleState = _multisampleState.AsPointer(),
			PDepthStencilState = _depthStencilState.AsPointer(),
			PColorBlendState = _colorBlendState.AsPointer(),
			PDynamicState = _dynamicState.AsPointer(),
			Layout = pipelineLayout,
			RenderPass = renderPass,
			Subpass = subpass,
			BasePipelineHandle = basePipeline,
			BasePipelineIndex = basePipelineIndex
		};

		Context.Vk.CreateGraphicsPipelines(Context.Device, PipelineManager.PipelineCache, 1, createInfo, null, out var pipeline);

		return pipeline;
	}
}
