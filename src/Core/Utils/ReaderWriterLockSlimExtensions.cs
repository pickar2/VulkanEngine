using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Core.Utils;

public static class ReaderWriterLockSlimExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ReadLockToken ReadLock(this ReaderWriterLockSlim readerWriterLockSlim) =>
		new(readerWriterLockSlim);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UpgradableReadLockToken UpgradableReadLock(this ReaderWriterLockSlim readerWriterLockSlim) =>
		new(readerWriterLockSlim);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static WriteLockToken WriteLock(this ReaderWriterLockSlim readerWriterLockSlim) =>
		new(readerWriterLockSlim);

	public readonly struct ReadLockToken : IDisposable
	{
		private readonly ReaderWriterLockSlim _readerWriterLockSlim;

		// ReSharper disable once UnusedMember.Global
		public ReadLockToken() => throw new NotSupportedException();

		public ReadLockToken(ReaderWriterLockSlim readerWriterLockSlim) =>
			(_readerWriterLockSlim = readerWriterLockSlim).EnterReadLock();

		public void Dispose() => _readerWriterLockSlim.ExitReadLock();
	}

	public readonly struct UpgradableReadLockToken : IDisposable
	{
		private readonly ReaderWriterLockSlim _readerWriterLockSlim;

		// ReSharper disable once UnusedMember.Global
		public UpgradableReadLockToken() => throw new NotSupportedException();

		public UpgradableReadLockToken(ReaderWriterLockSlim readerWriterLockSlim) =>
			(_readerWriterLockSlim = readerWriterLockSlim).EnterUpgradeableReadLock();

		public WriteLockToken WriteLock() => new(_readerWriterLockSlim);
		public void Dispose() => _readerWriterLockSlim.ExitUpgradeableReadLock();
	}

	public readonly struct WriteLockToken : IDisposable
	{
		private readonly ReaderWriterLockSlim _readerWriterLockSlim;

		// ReSharper disable once UnusedMember.Global
		public WriteLockToken() => throw new NotSupportedException();

		public WriteLockToken(ReaderWriterLockSlim readerWriterLockSlim) =>
			(_readerWriterLockSlim = readerWriterLockSlim).EnterWriteLock();

		public void Dispose() => _readerWriterLockSlim.ExitWriteLock();
	}
}
