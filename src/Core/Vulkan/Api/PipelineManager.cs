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
	public static readonly ReCreator<PipelineCache> PipelineCache;
	public static readonly Dictionary<string, AutoPipeline> AutoPipelines = new();

	static PipelineManager() => PipelineCache = ReCreate.InDevice.Auto(() => CreatePipelineCache(), cache => cache.Dispose());

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

	public static VulkanPipeline CreateComputePipeline(VulkanShader shader, DescriptorSetLayout[] layouts, PipelineCreateFlags pipelineFlags = 0,
		PushConstantRange[]? pushConstantRanges = null)
	{
		pushConstantRanges ??= Array.Empty<PushConstantRange>();

		var shaderStage = new PipelineShaderStageCreateInfo
		{
			SType = StructureType.PipelineShaderStageCreateInfo,
			Stage = ShaderStageFlags.ComputeBit,
			Module = shader.VulkanModule,
			PName = StringManager.GetStringPtr<byte>("main")
		};

		var layoutCreateInfo = new PipelineLayoutCreateInfo
		{
			SType = StructureType.PipelineLayoutCreateInfo,
			SetLayoutCount = (uint) layouts.Length,
			PSetLayouts = layouts[0].AsPointer(),
			PushConstantRangeCount = (uint) pushConstantRanges.Length
		};
		if (pushConstantRanges.Length > 0) layoutCreateInfo.PPushConstantRanges = pushConstantRanges[0].AsPointer();

		Context.Vk.CreatePipelineLayout(Context.Device, &layoutCreateInfo, null, out var layout);

		var createInfo = new ComputePipelineCreateInfo
		{
			SType = StructureType.ComputePipelineCreateInfo,
			Stage = shaderStage,
			Layout = layout,
			Flags = pipelineFlags
		};

		Context.Vk.CreateComputePipelines(Context.Device, PipelineCache, 1, &createInfo, null, out var pipeline);

		return new VulkanPipeline
		{
			Pipeline = pipeline,
			PipelineLayout = layout
		};
	}

	public static GraphicsPipelineBuilder GraphicsBuilder() => new();
}

public class AutoPipeline
{
	public readonly string Name;
	public readonly GraphicsPipelineBuilder Builder;
	public bool IsChanged { get; set; }
	private Pipeline _pipeline;

	public Pipeline Pipeline
	{
		get
		{
			if (_pipeline.Handle != default && !IsChanged && !Builder.IsChanged) return _pipeline;
			IsChanged = false;
			var old = _pipeline;
			if (old.Handle != default) ExecuteOnce.InSwapchain.AfterDispose(() => old.Dispose());

			_pipeline = Builder.Build();

			Debug.SetObjectName(_pipeline.Handle, ObjectType.Pipeline, $"Pipeline {Name}");
			return _pipeline;
		}
	}

	public AutoPipeline(string name, GraphicsPipelineBuilder builder)
	{
		Name = name;
		Builder = builder;
		Context.DeviceEvents.BeforeDispose += () => Dispose();
		PipelineManager.AutoPipelines.Add(name, this);

		if (!Context.State.AllowShaderWatchers) return;
		foreach ((var shaderKind, string? shaderPath) in Builder.Shaders)
		{
			ShaderWatchers.AddWatcherCallback(shaderPath, $"{shaderKind}.{Builder.GetHashCode()}", () =>
			{
				IsChanged = true;
			});

			Context.DeviceEvents.AfterCreate += () =>
			{
				ShaderWatchers.AddWatcherCallback(shaderPath, $"{shaderKind}.{Builder.GetHashCode()}", () =>
				{
					IsChanged = true;
				});
			};
		}
	}

	public void Dispose()
	{
		if (_pipeline.Handle != default) _pipeline.Dispose();
		_pipeline.Handle = default;
	}

	public static implicit operator Pipeline(AutoPipeline autoPipeline) => autoPipeline.Pipeline;
}

public unsafe class GraphicsPipelineBuilder
{
	public bool IsChanged { get; private set; }

	private PipelineCreateFlags _pipelineCreateFlags;

	private int _instanceInputs;
	public readonly Dictionary<ShaderKind, string> Shaders = new();

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

	private ReCreator<PipelineLayout> _pipelineLayout;
	private ReCreator<RenderPass> _renderPass;
	private uint _subpass;
	private Pipeline _basePipeline;
	private int _basePipelineIndex;

	public GraphicsPipelineBuilder() => ResetToDefault();

	public AutoPipeline AutoPipeline(string name) => new(name, this);

	public Pipeline Build()
	{
		IsChanged = false;

		var shaderStagesArr = Shaders.Select(pair => ShaderManager.GetOrCreate(pair.Value, pair.Key).ShaderCreateInfo()).ToArray();

		PipelineVertexInputStateCreateInfo vertexInputState = default;
		if (Shaders.TryGetValue(ShaderKind.VertexShader, out string? vertexShader))
		{
			vertexInputState = ReflectUtils.VertexInputStateFromShader(ShaderManager.GetOrCreate(vertexShader, ShaderKind.VertexShader), _instanceInputs);
		}

		_viewportState = new PipelineViewportStateCreateInfo
		{
			SType = StructureType.PipelineViewportStateCreateInfo,
			ViewportCount = 1,
			PViewports = _viewport.AsPointer(),
			ScissorCount = 1,
			PScissors = _scissor.AsPointer()
		};

		_colorBlendState.AttachmentCount = (uint) _colorBlendAttachmentStates.Count;
		_colorBlendState.PAttachments = _colorBlendAttachmentStates.AsPointer();

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
			Layout = _pipelineLayout,
			RenderPass = _renderPass,
			Subpass = _subpass,
			BasePipelineHandle = _basePipeline,
			BasePipelineIndex = _basePipelineIndex
		};

		Context.Vk.CreateGraphicsPipelines(Context.Device, PipelineManager.PipelineCache, 1, createInfo, null, out var pipeline);

		return pipeline;
	}

	public GraphicsPipelineBuilder With(ReCreator<PipelineLayout> pipelineLayout, ReCreator<RenderPass> renderPass, uint subpass = 0,
		Pipeline basePipeline = default,
		int basePipelineIndex = -1)
	{
		IsChanged = true;

		_pipelineLayout = pipelineLayout;
		_renderPass = renderPass;
		_subpass = subpass;
		_basePipeline = basePipeline;
		_basePipelineIndex = basePipelineIndex;

		return this;
	}

	public GraphicsPipelineBuilder WithShader(VulkanShader shader)
	{
		IsChanged = true;

		Shaders[shader.ShaderKind] = shader.Path;

		return this;
	}

	public GraphicsPipelineBuilder WithShader(string path, ShaderKind shaderKind)
	{
		IsChanged = true;

		Shaders[shaderKind] = ShaderManager.NormalizeShaderPath(path);

		return this;
	}

	public GraphicsPipelineBuilder SetInstanceInputs(int instanceInputs)
	{
		_instanceInputs = instanceInputs;

		return this;
	}

	public GraphicsPipelineBuilder ResetToDefault()
	{
		IsChanged = true;

		_pipelineCreateFlags = 0;

		_instanceInputs = 0;
		Shaders.Clear();

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
		IsChanged = true;

		_dynamicState = dynamicState;

		return this;
	}

	public GraphicsPipelineBuilder DynamicState(SpanAction<PipelineDynamicStateCreateInfo> updater)
	{
		IsChanged = true;

		updater.Invoke(ref _dynamicState);

		return this;
	}

	public GraphicsPipelineBuilder SetColorBlendState(PipelineColorBlendStateCreateInfo colorBlendState)
	{
		IsChanged = true;

		_colorBlendState = colorBlendState;

		return this;
	}

	public GraphicsPipelineBuilder ColorBlendState(SpanAction<PipelineColorBlendStateCreateInfo> updater)
	{
		IsChanged = true;

		updater.Invoke(ref _colorBlendState);

		return this;
	}

	public GraphicsPipelineBuilder AddColorBlendAttachment(PipelineColorBlendAttachmentState attachmentState)
	{
		IsChanged = true;

		_colorBlendAttachmentStates.Add(attachmentState);

		return this;
	}

	public GraphicsPipelineBuilder AddColorBlendAttachmentOneMinusSrcAlpha()
	{
		IsChanged = true;

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
		IsChanged = true;

		var attachmentState = new PipelineColorBlendAttachmentState
		{
			BlendEnable = false
		};

		_colorBlendAttachmentStates.Add(attachmentState);

		return this;
	}

	public GraphicsPipelineBuilder SetDepthStencilState(PipelineDepthStencilStateCreateInfo depthStencilState)
	{
		IsChanged = true;

		_depthStencilState = depthStencilState;

		return this;
	}

	public GraphicsPipelineBuilder DepthStencilState(SpanAction<PipelineDepthStencilStateCreateInfo> updater)
	{
		IsChanged = true;

		updater.Invoke(ref _depthStencilState);

		return this;
	}

	public GraphicsPipelineBuilder SetMultisampleState(PipelineMultisampleStateCreateInfo multisampleState)
	{
		IsChanged = true;

		_multisampleState = multisampleState;

		return this;
	}

	public GraphicsPipelineBuilder MultisampleState(SpanAction<PipelineMultisampleStateCreateInfo> updater)
	{
		IsChanged = true;

		updater.Invoke(ref _multisampleState);

		return this;
	}

	public GraphicsPipelineBuilder SetRasterizationState(PipelineRasterizationStateCreateInfo rasterizationState)
	{
		IsChanged = true;

		_rasterizationState = rasterizationState;

		return this;
	}

	public GraphicsPipelineBuilder RasterizationState(SpanAction<PipelineRasterizationStateCreateInfo> updater)
	{
		IsChanged = true;

		updater.Invoke(ref _rasterizationState);

		return this;
	}

	public GraphicsPipelineBuilder SetViewport(Viewport viewport)
	{
		IsChanged = true;

		_viewport = viewport;

		return this;
	}

	public GraphicsPipelineBuilder Viewport(SpanAction<Viewport> updater)
	{
		IsChanged = true;

		updater.Invoke(ref _viewport);

		return this;
	}

	public GraphicsPipelineBuilder SetScissor(Rect2D scissor)
	{
		IsChanged = true;

		_scissor = scissor;

		return this;
	}

	public GraphicsPipelineBuilder Scissor(SpanAction<Rect2D> updater)
	{
		IsChanged = true;

		updater.Invoke(ref _scissor);

		return this;
	}

	public GraphicsPipelineBuilder SetViewportState(PipelineViewportStateCreateInfo viewportState)
	{
		IsChanged = true;

		_viewportState = viewportState;

		return this;
	}

	public GraphicsPipelineBuilder ViewportState(SpanAction<PipelineViewportStateCreateInfo> updater)
	{
		IsChanged = true;

		updater.Invoke(ref _viewportState);

		return this;
	}

	public GraphicsPipelineBuilder SetViewportAndScissorFromSize(Vector2<uint> size)
	{
		IsChanged = true;

		_viewport = new Viewport
		{
			X = 0, Y = 0,
			Width = size.X, Height = size.Y,
			MinDepth = 0, MaxDepth = 1
		};

		_scissor = new Rect2D(new Offset2D(0, 0), new Extent2D(size.X, size.Y));

		return this;
	}

	public GraphicsPipelineBuilder PipelineCreateFlags(SpanAction<PipelineCreateFlags> updater)
	{
		IsChanged = true;

		updater.Invoke(ref _pipelineCreateFlags);

		return this;
	}

	public GraphicsPipelineBuilder SetPipelineCreateFlags(PipelineCreateFlags flags)
	{
		IsChanged = true;

		_pipelineCreateFlags = flags;

		return this;
	}

	public override int GetHashCode() =>
		HashCode.Combine(string.GetHashCode(string.Concat(Shaders.Values)), _renderPass.Value.Handle, _pipelineLayout.Value.Handle, _subpass);
}
