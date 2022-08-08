using System;

namespace Core.Vulkan.Utility;

public delegate void SpanAction<T>(Span<T> span);
