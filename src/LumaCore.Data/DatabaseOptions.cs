// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Configuration;
using LumaCore.Data.Initialization;

namespace LumaCore.Data;

/// <summary>
/// Provides configuration settings for database connectivity in LumaCore.
/// </summary>
/// <remarks>
///     <para>
///     This configuration is typically loaded from <c>appsettings.json</c> under the section specified by
///     <see cref="SectionName"/>. It supports multiple database providers for flexible deployment scenarios.
///     </para>
///     <para>
///     Values are typically bound via the options pattern and should be validated during startup.
///     Database provider setup and runtime behavior are configured by <see cref="ServiceRegistration"/>.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     "Database": {
///         "Provider": "sqlite",
///         "ConnectionString": "Data Source=lumacore.db",
///         "HealthQuery": "SELECT 1",
///         "AutoCreate": true,
///         "AutoMigration": {
///             "Enabled": true,
///             "CreateBackupBeforeMigration": true,
///             "RestoreOnFailure": true,
///             "BackupRetentionDays": 7,
///             "BackupDirectory": ""
///         },
///         "Recovery": {
///             "Enabled": true,
///             "PollingIntervalSeconds": 10,
///             "FailureThreshold": 3,
///             "FailureWindowSeconds": 30
///         },
///         "PreferCompiledHotPathQueries": false,
///         "UserDeletion": {
///             "RedactMessages": true,
///             "DeletePrivateConversations": true
///         },
///         "CleanupConversationsWithNoUsersOnStartup": true,
///         "StoreFullPrompts": false,
///         "EncryptionKey": "",
///         "PreviousEncryptionKeys": [],
///         "RequireSnapshotIsolationForExport": false
///     }
///     </code>
/// </example>
public sealed class DatabaseOptions : IValidatableObject
{
	/// <summary>
	/// The configuration section name for database options.
	/// </summary>
	/// <remarks>
	/// Used for binding and diagnostics.
	/// </remarks>
	public const string SectionName = "Database";

	/// <summary>
	/// Provides the error message indicating that the database connection string configuration is required.
	/// </summary>
	/// <remarks>
	/// This constant is used when the application detects that the <c>Database:ConnectionString</c> setting is missing.
	/// The message guides users to set the configuration key or the corresponding environment variable.
	/// </remarks>
	private const string ConnectionStringRequiredError =
		"Database:ConnectionString must be configured. Set configuration key 'Database:ConnectionString' or environment variable 'Database__ConnectionString'.";

	/// <summary>
	/// Represents the error message displayed when the database encryption key does not meet the minimum required length
	/// of 32 characters.
	/// </summary>
	/// <remarks>
	/// This message advises using a long, random secret for the encryption key and cautions against committing it to
	/// source control. Ensuring the encryption key meets the minimum length is critical for maintaining database security.
	/// </remarks>
	private const string EncryptionKeyMinLengthError =
		"Database:EncryptionKey must be at least 32 characters long. Use a long, random secret and do not commit it to source control.";

	/// <summary>
	/// Provides the error message indicating that the database encryption key configuration is required.
	/// </summary>
	/// <remarks>
	/// This constant is used when the application detects that the <c>Database:EncryptionKey</c> setting is missing.
	/// The message guides users to set the configuration key or the corresponding environment variable to enable encryption
	/// features.
	/// </remarks>
	private const string EncryptionKeyRequiredError =
		"Database:EncryptionKey must be configured. Set configuration key 'Database:EncryptionKey' or environment variable 'Database__EncryptionKey'.";

	/// <summary>
	/// Provides the error message indicating that the database provider configuration is required.
	/// </summary>
	/// <remarks>
	/// This constant is used when the application detects that the <c>Database:Provider</c> setting is missing.
	/// The message guides users to set the configuration key or the corresponding environment variable.
	/// </remarks>
	private const string ProviderRequiredError =
		"Database:Provider must be configured. Set configuration key 'Database:Provider' or environment variable 'Database__Provider'.";

	/// <summary>
	/// Gets or sets auto-migration settings (apply migrations, backups, restore-on-failure).
	/// </summary>
	public AutoMigrationOptions AutoMigration
	{
		get;
		init => field = value ?? throw new ArgumentNullException(nameof(value));
	} = new();

	/// <summary>
	/// Gets or sets settings for automatic database recovery (self-healing).
	/// </summary>
	public RecoveryOptions Recovery
	{
		get;
		init => field = value ?? throw new ArgumentNullException(nameof(value));
	} = new();

	/// <summary>
	/// Gets or sets whether the database schema should be created automatically on first start.
	/// </summary>
	/// <remarks>
	///     <para>
	///     When <see langword="true"/> and the database is empty (no migrations applied yet), all migrations
	///     are applied to create the initial schema. This is the typical setup for new installations.
	///     </para>
	///     <para>
	///     When <see langword="false"/> and the database is empty, the application will log an error and exit.
	///     Use this setting if you prefer to run migrations manually before first start.
	///     </para>
	/// </remarks>
	public bool AutoCreate { get; set; } = true;

	/// <summary>
	/// Gets or sets settings for user deletion behavior (redaction, private conversation cleanup).
	/// </summary>
	public UserDeletionOptions UserDeletion
	{
		get;
		init => field = value ?? throw new ArgumentNullException(nameof(value));
	} = new();

	/// <summary>
	/// Gets or sets whether to clean up conversations that have no user participants on application startup.
	/// </summary>
	/// <remarks>
	/// Conversations without user participants are considered unreachable/inconsistent data and may occur due to bugs,
	/// manual database edits, or interrupted maintenance workflows.
	/// </remarks>
	public bool CleanupConversationsWithNoUsersOnStartup { get; set; } = true;

	/// <summary>
	/// Gets or sets the connection string for the database.
	/// </summary>
	/// <remarks>
	/// The format depends on the selected <see cref="Provider"/>.
	/// Treat this value as sensitive (may contain credentials) and avoid logging it.
	/// </remarks>
	/// <example>
	///     <para>SQLite: <c>Data Source=lumacore.db</c></para>
	///     <para>PostgreSQL: <c>Host=localhost;Database=lumacore;Username=app;Password=secret</c></para>
	///     <para>
	///     SQL Server: <c>Server=localhost;Database=lumacore;User Id=app;Password=secret;TrustServerCertificate=true</c>
	///     </para>
	///     <para>MySQL: <c>Server=localhost;Database=lumacore;User=app;Password=secret</c></para>
	/// </example>
	[Required(ErrorMessage = ConnectionStringRequiredError)]
	public string ConnectionString { get; set; } = "Data Source=lumacore.db";

	/// <summary>
	/// Gets or sets the lightweight SQL query executed during health checks.
	/// </summary>
	/// <remarks>
	/// For most relational databases, <c>SELECT 1</c> is sufficient to verify connectivity.
	/// Keep this query lightweight and side-effect free.
	/// </remarks>
	public string HealthQuery { get; set; } = "SELECT 1";

	/// <summary>
	/// Gets or sets whether LumaCore should prefer EF Core compiled queries for selected read hot paths.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Default:</b> <see langword="false"/>.
	///     </para>
	///     <para>
	///     When enabled, LumaCore may use pre-compiled EF Core query delegates for frequently executed lookups such as
	///     existence checks and simple key-based retrieval. This can reduce EF Core's per-call overhead (query
	///     translation / cache lookups) on high-traffic installations.
	///     </para>
	///     <para>
	///     <b>Important trade-off:</b> EF Core compiled queries do not support passing a <see cref="CancellationToken"/>
	///     into the compiled delegate. That means cancellation becomes "best effort" at a higher layer (caller stops
	///     awaiting), but the underlying database operation might still run to completion.
	///     </para>
	///     <para>
	///     <b>When to enable:</b> Consider enabling this only when you have measured EF Core query overhead as a
	///     bottleneck (e.g. multi-user server deployments with high request rates).
	///     </para>
	///     <para>
	///     <b>When to keep disabled:</b> For interactive UIs (Blazor) where requests are frequently cancelled due to
	///     navigation, re-rendering, or rapid user input, keeping this disabled improves responsiveness and reduces
	///     unnecessary "ghost work".
	///     </para>
	/// </remarks>
	public bool PreferCompiledHotPathQueries { get; set; } = false;

	/// <summary>
	/// Gets or sets the database provider to use.
	/// </summary>
	/// <remarks>
	///     <para>Supported values:</para>
	///     <list type="bullet">
	///         <item><c>sqlite</c> — SQLite, default for development and small deployments</item>
	///         <item><c>postgresql</c> — PostgreSQL, recommended for production</item>
	///         <item><c>sqlserver</c> — Microsoft SQL Server</item>
	///     </list>
	///     <para>
	///     <c>mysql</c> (MySQL / MariaDB via Pomelo) is recognized but <b>temporarily unavailable</b>:
	///     <c>Pomelo.EntityFrameworkCore.MySql</c> has not yet released an EF Core 10 compatible version.
	///     Selecting <c>mysql</c> will fail fast at startup with a descriptive error. Track progress at
	///     <see href="https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues"/>.
	///     </para>
	/// </remarks>
	[Required(ErrorMessage = ProviderRequiredError)]
	public string Provider { get; set; } = "sqlite";

	/// <summary>
	/// Gets or sets whether LumaCore should persist the full prompt used for AI message generation.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Default:</b> <see langword="false"/>.
	///     </para>
	///     <para>
	///     When enabled, <see cref="Entities.MessageGenerationMetadataEntity.FullPrompt"/> may be stored for diagnostics and
	///     reproducibility.
	///     </para>
	///     <para>
	///     This can include system prompts, conversation history, tool outputs, and user content and should be treated as
	///     potentially sensitive. Consider enabling this only in development or for short-lived troubleshooting.
	///     </para>
	/// </remarks>
	public bool StoreFullPrompts { get; set; } = false;

	/// <summary>
	/// Gets or sets the encryption key used to protect encrypted fields stored in the database.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This key is used to encrypt and decrypt sensitive values persisted in the database (e.g.
	///     <see cref="Entities.ModelEndpointEntity.EncryptedCredentials"/>).
	///     </para>
	///     <para>
	///     This value is bound from configuration key <c>Database:EncryptionKey</c>
	///     (or environment variable <c>Database__EncryptionKey</c>). In production this should be provided via environment
	///     variables or a dedicated secrets mechanism (for example Docker secrets or a cloud secret store).
	///     </para>
	///     <para>
	///     If the key is changed, previously encrypted values may become unreadable unless key rotation is implemented.
	///     </para>
	/// </remarks>
	[Secret]
	[Required(AllowEmptyStrings = false, ErrorMessage = EncryptionKeyRequiredError)]
	[MinLength(32, ErrorMessage = EncryptionKeyMinLengthError)]
	public string EncryptionKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets previous encryption keys available for decrypting existing stored values.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This list enables key rotation: new values are encrypted using <see cref="EncryptionKey"/>, while existing values
	///     can still be decrypted using entries in this list.
	///     </para>
	///     <para>
	///     Provide these values via environment variables or a dedicated secrets mechanism. Once all stored values have been
	///     re-encrypted with the current key, old keys can be removed.
	///     </para>
	/// </remarks>
	[Secret]
	public List<string> PreviousEncryptionKeys { get; set; } = [];

	/// <summary>
	/// Gets or sets whether database exports should require snapshot isolation for consistency guarantees.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Default:</b> <see langword="false"/>.
	///     </para>
	///     <para>
	///     When <see langword="true"/>, exports will fail if the database provider does not support or has not
	///     enabled snapshot isolation. This ensures that the exported data represents a consistent point-in-time
	///     snapshot, preventing foreign key violations when restoring to another database.
	///     </para>
	///     <para>
	///     When <see langword="false"/>, exports will fall back to a less strict isolation level (e.g., Read Committed)
	///     and log a warning. The export may be inconsistent if data is modified concurrently.
	///     </para>
	///     <para>
	///     <b>SQL Server:</b> Requires <c>ALTER DATABASE [DbName] SET ALLOW_SNAPSHOT_ISOLATION ON</c>.
	///     </para>
	///     <para>
	///     <b>PostgreSQL:</b> Uses Repeatable Read by default, which provides similar guarantees.
	///     </para>
	///     <para>
	///     <b>MySQL:</b> InnoDB uses Repeatable Read by default with MVCC (provides consistent reads).
	///     </para>
	///     <para>
	///     <b>SQLite:</b> Always consistent (single-writer model).
	///     </para>
	/// </remarks>
	public bool RequireSnapshotIsolationForExport { get; set; } = false;

	/// <inheritdoc/>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (AutoMigration is { RestoreOnFailure: true, CreateBackupBeforeMigration: false })
		{
			yield return new ValidationResult(
				"Database:AutoMigration:RestoreOnFailure is enabled but " +
				"Database:AutoMigration:CreateBackupBeforeMigration is disabled. " +
				"Automatic restore requires a backup to restore from. Either enable " +
				"CreateBackupBeforeMigration or disable RestoreOnFailure.",
				[
					$"{nameof(AutoMigration)}.{nameof(AutoMigrationOptions.RestoreOnFailure)}",
					$"{nameof(AutoMigration)}.{nameof(AutoMigrationOptions.CreateBackupBeforeMigration)}"
				]);
		}

		// Validate BackupDirectory when backup creation is enabled and a custom path is configured.
		// This catches obvious misconfigurations (invalid characters, whitespace-only paths) at startup
		// rather than at migration time, where the error would be harder to diagnose.
		if (AutoMigration is { CreateBackupBeforeMigration: true, BackupDirectory: { Length: > 0 } backupDir })
		{
			if (string.IsNullOrWhiteSpace(backupDir))
			{
				yield return new ValidationResult(
					"Database:AutoMigration:BackupDirectory is set but contains only whitespace. " +
					"Provide a valid directory path or leave empty to use the default backup location.",
					[$"{nameof(AutoMigration)}.{nameof(AutoMigrationOptions.BackupDirectory)}"]);
			}
			else
			{
				// Path.GetFullPath throws on invalid characters and other syntactic issues.
				// Cannot yield from a catch block, so capture the error and yield afterwards.
				string? pathError = null;
				try
				{
					Path.GetFullPath(backupDir);
				}
				catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
				{
					pathError = ex.Message;
				}

				if (pathError is not null)
				{
					yield return new ValidationResult(
						$"Database:AutoMigration:BackupDirectory contains an invalid path: '{backupDir}'. {pathError}",
						[$"{nameof(AutoMigration)}.{nameof(AutoMigrationOptions.BackupDirectory)}"]);
				}
			}
		}
	}

	/// <summary>
	/// Configuration settings for user deletion behavior.
	/// </summary>
	/// <remarks>
	/// These settings control what happens to a user's data when their account is deleted. They are designed
	/// to balance data minimization (privacy) with preserving conversation context for other participants.
	/// </remarks>
	public sealed class UserDeletionOptions
	{
		/// <summary>
		/// Gets or sets whether deleting a user should also delete all private conversations belonging to their participant.
		/// </summary>
		/// <remarks>
		/// A private conversation is defined as a conversation that contains exactly one user participant (the user being
		/// deleted) and any number of personas.
		/// </remarks>
		public bool DeletePrivateConversations { get; set; } = true;

		/// <summary>
		/// Gets or sets whether deleting a user should also redact all messages authored by their participant.
		/// </summary>
		/// <remarks>
		/// When enabled, user deletion will remove message content (via redaction) to minimize personal data while
		/// preserving conversation structure for other participants.
		/// </remarks>
		public bool RedactMessages { get; set; } = true;
	}

	/// <summary>
	/// Configuration settings for automatic migration behavior.
	/// </summary>
	/// <remarks>
	/// Controls three aspects of database schema management: applying pending migrations on startup,
	/// creating backups before migration attempts, and restoring from backup when migrations fail.
	/// </remarks>
	public sealed class AutoMigrationOptions
	{
		/// <summary>
		/// Gets or sets whether pending migrations should be applied automatically on existing databases.
		/// </summary>
		/// <remarks>
		///     <para>
		///     When <see langword="true"/>, pending migrations are applied automatically when the application starts.
		///     This keeps the database schema up-to-date with the application version.
		///     </para>
		///     <para>
		///     When <see langword="false"/> and pending migrations exist, the application will log an error and refuse to start.
		///     This fail-early behavior prevents the application from running with an inconsistent database schema.
		///     Use this setting in production environments where you want to review and apply migrations manually before
		///     restarting.
		///     </para>
		///     <para>
		///     Note: This setting only affects existing databases with at least one migration already applied.
		///     For initial schema creation, see <see cref="DatabaseOptions.AutoCreate"/>.
		///     </para>
		/// </remarks>
		public bool Enabled { get; set; } = true;

		/// <summary>
		/// Gets or sets whether an automatic backup should be created before applying migrations.
		/// </summary>
		/// <remarks>
		///     <para>
		///     When <see langword="true"/> and migrations are about to be applied, a backup is created first.
		///     The backup is created as a LumaCore Shuttle file using the data porting pipeline.
		///     </para>
		///     <para>
		///     When enabled, startup will fail fast if the backup cannot be created.
		///     </para>
		/// </remarks>
		public bool CreateBackupBeforeMigration { get; set; } = true;

		/// <summary>
		/// Gets or sets whether a LumaCore Shuttle backup should be restored when an automatic migration fails.
		/// </summary>
		/// <remarks>
		///     <para>
		///     If backup creation fails while <see cref="CreateBackupBeforeMigration"/> is enabled, application startup
		///     aborts before applying migrations. In that case, no restore is needed because no migration attempt ran.
		///     </para>
		///     <para>
		///     <b>When enabled (<see langword="true"/>, default):</b> A failed migration run attempts an automatic restore
		///     from the LumaCore Shuttle backup to bring the database back to the pre-migration state. This minimizes downtime and
		///     prevents data loss from partially applied migrations.
		///     </para>
		///     <para>
		///     <b>When disabled (<see langword="false"/>):</b> A failed migration leaves the database in a partially
		///     migrated, potentially inconsistent state. Application startup aborts with an exception. Manual intervention
		///     is required: either restore the LumaCore Shuttle backup manually (if <see cref="CreateBackupBeforeMigration"/> was
		///     enabled) or fix the database schema manually.
		///     </para>
		/// </remarks>
		public bool RestoreOnFailure { get; set; } = true;

		/// <summary>
		/// Gets or sets the number of days to retain automatic backup files.
		/// </summary>
		/// <remarks>
		/// Backup files older than this value are automatically deleted after successful migrations. Set to <c>0</c>
		/// to keep backups indefinitely.
		/// </remarks>
		public int BackupRetentionDays { get; set; } = 7;

		/// <summary>
		/// Gets or sets the directory where automatic database backups are stored.
		/// </summary>
		/// <remarks>
		///     <para>
		///     When <see langword="null"/> or empty, the default backup directory is derived at runtime
		///     (OS temp path, e.g., <c>C:\Users\...\AppData\Local\Temp\LumaCore\backups</c>).
		///     </para>
		///     <para>
		///     The value can be an absolute path or a relative path. Relative paths are resolved against the application
		///     base directory (<see cref="AppContext.BaseDirectory"/>), not the current working directory. This ensures
		///     predictable behavior across different hosting environments (systemd, Docker, Windows services, etc.).
		///     </para>
		///     <para>
		///         <b>Examples:</b>
		///     </para>
		///     <list type="bullet">
		///         <item>Absolute: <c>/var/lib/lumacore/backups</c> or <c>C:\ProgramData\LumaCore\backups</c></item>
		///         <item>Relative: <c>backups</c> (resolves to <c>{AppBaseDirectory}/backups</c>)</item>
		///     </list>
		/// </remarks>
		public string? BackupDirectory { get; set; } = null;
	}

	/// <summary>
	/// Configuration settings for automatic database recovery (self-healing).
	/// </summary>
	/// <remarks>
	///     <para>
	///     When database initialization fails during startup or the connection is lost during runtime, this feature
	///     enables automatic background recovery without requiring a manual restart.
	///     </para>
	///     <para>
	///     Recovery includes the full initialization flow: connectivity check, migrations (if enabled), cleanup, and
	///     seeding. This ensures the database is in a fully consistent state before accepting requests.
	///     </para>
	/// </remarks>
	public sealed class RecoveryOptions
	{
		/// <summary>
		/// Gets or sets whether automatic database recovery is enabled.
		/// </summary>
		/// <remarks>
		///     <para>
		///     When <see langword="true"/> (default), a background service monitors database health and automatically
		///     attempts recovery when initialization fails or the connection is lost.
		///     </para>
		///     <para>
		///     <b>Startup failures:</b> If <see cref="DatabaseInitializer"/> fails, the background service retries the
		///     full initialization flow (migrations, cleanup, seeding) until it succeeds.
		///     </para>
		///     <para>
		///     <b>Runtime disconnections:</b> If <see cref="DatabaseConnectionInterceptor"/> detects a connection loss,
		///     the background service polls for connectivity and runs initialization to ensure the database is ready.
		///     </para>
		/// </remarks>
		public bool Enabled { get; set; } = true;

		/// <summary>
		/// Gets or sets the polling interval in seconds for the recovery background service.
		/// </summary>
		/// <remarks>
		///     <para>
		///     This interval controls how frequently the background service checks database health and attempts
		///     recovery when the database is in a <see cref="DatabaseInitializationState.Failed"/> or
		///     <see cref="DatabaseInitializationState.Disconnected"/> state.
		///     </para>
		///     <para>
		///     A fixed interval is used (no exponential backoff) to ensure quick recovery when the database becomes
		///     available. The default of 10 seconds provides a good balance between responsiveness and minimal overhead.
		///     </para>
		/// </remarks>
		[Range(1, 3600, ErrorMessage = "Database:Recovery:PollingIntervalSeconds must be between 1 and 3600 seconds.")]
		public int PollingIntervalSeconds { get; set; } = 10;

		/// <summary>
		/// Gets or sets the number of failures required within <see cref="FailureWindowSeconds"/> before
		/// the circuit breaker trips and sets the database status to disconnected.
		/// </summary>
		/// <remarks>
		/// This threshold prevents transient network glitches from triggering a full 503 response.
		/// Only after this many failures within the time window does the system consider the database
		/// truly unavailable.
		/// </remarks>
		[Range(1, 100, ErrorMessage = "Database:Recovery:FailureThreshold must be between 1 and 100.")]
		public int FailureThreshold { get; set; } = 3;

		/// <summary>
		/// Gets or sets the time window in seconds for counting failures toward the <see cref="FailureThreshold"/>.
		/// </summary>
		/// <remarks>
		///     <para>
		///     Failures older than this window are not counted. This allows the system to recover from
		///     brief outages without permanently remembering past failures.
		///     </para>
		///     <para>
		///     For example, with a threshold of 3 and a window of 30 seconds, the database must fail
		///     3 times within any 30-second period before the circuit breaker trips.
		///     </para>
		/// </remarks>
		[Range(1, 300, ErrorMessage = "Database:Recovery:FailureWindowSeconds must be between 1 and 300 seconds.")]
		public int FailureWindowSeconds { get; set; } = 30;
	}
}
