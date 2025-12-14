using AgValoniaGPS.Services.Track;

Console.WriteLine("Running TrackGuidanceService Tests...\n");

var (success, results) = TrackGuidanceServiceTests.RunAllTests();

Console.WriteLine(results);

Environment.Exit(success ? 0 : 1);
