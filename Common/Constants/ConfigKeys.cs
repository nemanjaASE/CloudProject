namespace Common.Constants
{
	public class ConfigKeys
	{
		public const string ConnectionString = "DataConnectionString";
		public const string VenvPath = $"C:\\Users\\night\\Desktop\\Master\\Cloud\\CloudProject\\DocumentProcessingService\\Scripts\\venv\\Scripts\\python.exe";
		public const string FilePath = $"C:\\Users\\night\\Desktop\\Master\\Cloud\\CloudProject\\DocumentProcessingService\\Scripts\\script.py";

		public const string AnalysisTableName = "AnalysisEdu";
		public const string UsersTableName = "UsersEdu";
		public const string CourseTableName = "CourseEdu";
		public const string ContainerBlobName = "documents";

		public const string ReliableQueue = "DocumentProcessingQueue";
		public const string ReliableDictionaryHistory = "HistoryDictionary";
		public const string ReliableDicitonaryDocument = "DocumentDicitonary";
		public const string ReliableDicitonaryUser = "UserDicitonary";
		public const string ReliableDictionaryRateLimit = "RateLimitDictionary";
		public const string ReliableDictionaryRateLimitSettings = "RateLimitSettingsDictionary";
		public const string ReliableCollectionCourse = "CourseCollection";
		public const string ReliableDictionarySettings = "SettingsDictionary";
		public const string ReliableDictionaryAnalysis = "AnalysisDictionary";
	}
}
