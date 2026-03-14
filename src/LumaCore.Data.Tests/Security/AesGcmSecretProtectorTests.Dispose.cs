// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Security;

using Xunit;

namespace LumaCore.Data.Tests.Security;

public sealed partial class AesGcmSecretProtectorTests
{
	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Dispose"/> can be called multiple times
	/// without throwing an exception (idempotent dispose pattern).
	/// </summary>
	[Fact]
	public void Dispose_WhenCalledMultipleTimes_DoesNotThrow()
	{
		// Arrange
		AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey, [AlternativeKey]);

		// Act - calling Dispose multiple times should not throw
		protector.Dispose();
		protector.Dispose();
		protector.Dispose();

		// Assert - implicit: test passes if no exception was thrown
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Dispose"/> clears key material,
	/// making subsequent operations fail with <see cref="ObjectDisposedException"/>.
	/// </summary>
	[Fact]
	public void Dispose_WhenCalled_PreventsSubsequentOperations()
	{
		// Arrange
		AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		string protectedValue = protector.Protect("test");

		// Act
		protector.Dispose();

		// Assert
		Assert.Throws<ObjectDisposedException>(() => protector.Protect("test"));
		Assert.Throws<ObjectDisposedException>(() => protector.Unprotect(protectedValue));
	}
}
