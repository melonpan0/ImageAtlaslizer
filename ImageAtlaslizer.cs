using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenCvSharp;

public class Config {
    public float AspectRatioHori { get; set; }
    public float AspectRatioVert { get; set; }
    public int AtlasImageWidth { get; set; }
    public int AtlasImageHeight { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public string? ExportFileType { get; set; }
    public int ExportQuality { get; set; }
}

public class ImageAtlaslizer {
    string[] ImageExtensionArray = ["jpeg", "jpg", "png", "webp"];

    public static void Main(string[] args) {
        // Error発生時、勝手に落ちない用
        try {
            ImageAtlaslizer Main = new ImageAtlaslizer();
            int result = Main.AtlaslizeImages(args);
        } catch (Exception e) {
            Console.WriteLine($"Error: {e.Message}");
            Console.WriteLine(e.StackTrace);
        } finally {
            // ガベージコレクタ手動起動
            GC.Collect();
            // GC.WaitForPendingFinalizers(); // 待機する必要はなし

            // 終了メッセージ
            Console.Write("Press Any Key to Exit... ");
            Console.ReadKey();
        }
    }

    public int AtlaslizeImages(string[] args) {
        // Config情報(Default値)
        float AspectRatioHori = 1f;
        float AspectRatioVert = 1.41421356f;
        int AtlasImageWidth = 2048;
        int AtlasImageHeight = 2048;
        int Row = 4;
        int Col = 4;
        string ExportFileType = "png";
        int ExportQuality = 5;

        Console.WriteLine("ImageAtlaslizer 1.1.0(23/12/01)   Written by melon.");
        Console.WriteLine("Program Start.");

        // Configファイル読み込み
        Config? Config = new Config();
        String ConfigFile = Directory.GetCurrentDirectory() + "\\config.json";
        /// ファイル読み込み
        if (File.Exists(ConfigFile)) {
            Console.WriteLine($"Read Config File: {ConfigFile}");
            using (StreamReader Reader = new StreamReader(ConfigFile)) {
                string ConfigText = Reader.ReadToEnd();
                Config = JsonSerializer.Deserialize<Config>(ConfigText);
            }
            if (Config != null) {
                // 値を移し替え
                AspectRatioHori = Config.AspectRatioHori;
                AspectRatioVert = Config.AspectRatioVert;
                AtlasImageWidth = Config.AtlasImageWidth;
                AtlasImageHeight = Config.AtlasImageHeight;
                Row = Config.Row;
                Col = Config.Col;
                ExportFileType = Config.ExportFileType;
                ExportQuality = Config.ExportQuality;
            } else {
                // 読み込み失敗
                Console.WriteLine("Read Config File: Failed. Use a default value instead.");
            }
        } else {
            // ファイルがなかった
            Console.WriteLine("Read Config File: Not Found. Use a default value instead.");
        }

        // Config値検証
        Boolean ConfigValidation = true;

        if (AspectRatioHori < 0 || AspectRatioVert < 0) {
            Console.WriteLine("[Error] Aspect Ratio is below 0.\n\tThe value must be Grater than 0.");
            Console.WriteLine($"\tAspectRatioHori: {AspectRatioHori}, AspectRatioVert: {AspectRatioVert}");
            ConfigValidation = false;
        }

        if (AtlasImageWidth < 1 || AtlasImageHeight < 1) {
            Console.WriteLine("[Error] Atlas Size is below 1.\n\tThe value must be Grater than or equal to 1.");
            Console.WriteLine($"\tAtlasImageWidth: {AtlasImageWidth}, AtlasImageHeight: {AtlasImageHeight}");
            ConfigValidation = false;
        }
        if (Row < 1 || Col < 1) {
            Console.WriteLine("[Error] Row or Col are below 1.\n\tThe value must be Grater than or equal to 1.");
            Console.WriteLine($"\tRow: {Row}, Col: {Col}");
            ConfigValidation = false;
        }
        if (Row > AtlasImageWidth || Col > AtlasImageHeight) {
            Console.WriteLine("[Error] There are too many requests for the output image size.\n\tIncrease the Atlas size or reduce the inputs.");
            Console.WriteLine($"\tAtlasImageWidth: {AtlasImageWidth}, Row: {Row}");
            Console.WriteLine($"\tAtlasImageHeight: {AtlasImageHeight}, Col: {Col}");
            ConfigValidation = false;
        }

        if (!ImageExtensionArray.Contains(ExportFileType)) {
            Console.WriteLine($"[Error] ExportFileType is Not Supporting. Support file: {String.Join(", ", ImageExtensionArray)}");
            Console.WriteLine($"\tExportFiletype: {ExportFileType}");
            ConfigValidation = false;
        }

        if (ExportQuality < 0 || 100 < ExportQuality) {
            Console.WriteLine("[Error] ExportQuality is Out of Range. Enter the value from 0 to 100.");
            Console.WriteLine($"\tExportQuality: {ExportQuality}");
            ConfigValidation = false;
        }

        if(ExportFileType.Equals("png") && 9 < ExportQuality) {
            Console.WriteLine("[Error] ExportQuality is Out of Range(png). Enter the value from 0 to 9.");
            Console.WriteLine($"\tExportQuality: {ExportQuality}");
            ConfigValidation = false;
        }

        // Config値がNGなら差し戻し
        if (ConfigValidation == false) {
            return -1;
        }

        // Config値出力
        Console.WriteLine($"Config Info[ExportSize]: {AtlasImageWidth}x{AtlasImageHeight}px");
        Console.WriteLine($"Config Info[Row, Col]: {Row}x{Col}");

        // 作業用変数用意
        // 最小公倍数で作業すると、最後のリサイズがきれいになる(はず)
        int ImageCount = Row * Col;
        int WorkingImageWidth = Lcm(AtlasImageWidth, Row);
        int WorkingImageHeight = Lcm(AtlasImageHeight, Col);
        int UnitImageWidth = WorkingImageWidth / Row;
        int UnitImageHeight = WorkingImageHeight / Col;

        Console.WriteLine($"Working Info[ImageCount]: {ImageCount}");
        Console.WriteLine($"Working Info[WorkingImage]: {WorkingImageWidth}x{WorkingImageHeight}px");
        Console.WriteLine($"Working Info[UnitImage]:{(AtlasImageWidth / Row)}x{(AtlasImageHeight / Col)}px");
        // 警告系(出力自体に問題はないから続行)
        if (WorkingImageWidth > 8192 || WorkingImageHeight > 8192) {
            Console.WriteLine("[Warning] WorkingImage size is TOO Large!!");
            Console.WriteLine("\tIt requires a large amount of RAM and may cause system instability.");
        }
        if (AtlasImageWidth % Row != 0) {
            Console.WriteLine("[Notice] UnitImage[Width] is NOT an integer value.");
            Console.WriteLine("\tWhen converted to Atlas, the image boundary may become blurred.");
        }
        if (AtlasImageHeight % Col != 0) {
            Console.WriteLine("[Notice] UnitImage[Height] is NOT an integer value.");
            Console.WriteLine("\tWhen converted to Atlas, the image boundary may become blurred.");
        }

        // 画像一覧読み込み
        List<String> ImagesPath = new List<String>();
        String ImportDir = Directory.GetCurrentDirectory() + "\\Import";
        // フォルダ確認
        if (!Directory.Exists(ImportDir)) {
            // なければフォルダを作成してreturn
            Directory.CreateDirectory(ImportDir);
            Console.WriteLine("[Error] Import Folder is NOT Found. So this program created \"Import\" Folder. Please Input Images this folder.");
            Console.WriteLine($"\tDirectory Path: {ImportDir}");
            return -1;
        }
        /// ファイル読み込み
        String[] FilePath = Directory.GetFiles(ImportDir);
        /// 拡張子が画像のもののみ抽出
        String pattern = @"\.(jpg|jpeg|png|bmp)";
        foreach (String path in FilePath) {
            String ext = Path.GetExtension(path);
            if(Regex.IsMatch(ext, pattern)) {
                ImagesPath.Add(path);
            }
        }

        // 読み込み&リサイズ
        Mat[] ResizeImages = new Mat[ImageCount];
        for (int i = 0; i < ImageCount; i++) {
            // ファイルが存在するか確認
            Mat Mat = new Mat();
            if (i < ImagesPath.Count && File.Exists(ImagesPath[i])) {
                Console.WriteLine($"Read File: {ImagesPath[i]}");
                Mat = new Mat(ImagesPath[i]);
            } else {
                // 空画像を作成
                Console.WriteLine($"Create Empty Image: [{i}]");
                Mat = new Mat(new Size(6, 8), MatType.CV_8UC3, new Scalar(0, 0, 0));
            }

            // アスペクト比が一致していないとき、空部分を埋める
            /// 理想値算出
            int ExpectedWidth = (int)Math.Round((double)Mat.Height / AspectRatioVert * AspectRatioHori);
            int ExpectedHeight = (int)Math.Round((double)Mat.Width / AspectRatioHori *  AspectRatioVert);
            /// 空白を埋める(padding)
            if (ExpectedWidth > Mat.Width) {
                Console.WriteLine($"Padding(Width): +{(ExpectedWidth - Mat.Width)}px");
                int paddingWidth = (int)Math.Round((double)(ExpectedWidth - Mat.Width) / 2);
                Mat = Mat.CopyMakeBorder(0, 0, paddingWidth, paddingWidth, BorderTypes.Constant, new Scalar(0, 0, 0));
            } else if (ExpectedHeight > Mat.Height) {
                Console.WriteLine($"Padding(Width): +{(ExpectedHeight - Mat.Height)}px");
                int paddingHeight = (int)Math.Round((double)(ExpectedHeight - Mat.Height) / 2);
                Mat = Mat.CopyMakeBorder(paddingHeight, paddingHeight, 0, 0, BorderTypes.Constant, new Scalar(0, 0, 0));
            }

            // リサイズ, 色々比較した結果Area補完がよかった
            Console.WriteLine($"Resize: {Mat.Width}x{Mat.Height}px -> {UnitImageWidth}x{UnitImageHeight}px");
            ResizeImages[i] = Mat.Resize(new Size(UnitImageWidth, UnitImageHeight), 0, 0, InterpolationFlags.Area);
        }

        // アトラス化
        Mat AtlasImage = new Mat(new Size(WorkingImageWidth, 0), MatType.CV_8UC3);
        for (int i = 0; i < Col; i++) {

            Mat TmpImage = new Mat(new Size(0, UnitImageHeight), MatType.CV_8UC3);
            // Concat(src1, src2, dst);
            for (int j = 0; j < Row; j++) {
                Console.WriteLine($"Atlasization: Image({i}, {j})");
                Cv2.HConcat(TmpImage, ResizeImages[Row * i + j], TmpImage);
            }
            Console.WriteLine($"Atlasization: Row[{i}]");
            Cv2.VConcat(AtlasImage, TmpImage, AtlasImage);
        }

        // 拡大して作業していた場合、リサイズ
        if (WorkingImageWidth > AtlasImageWidth || WorkingImageHeight > AtlasImageHeight) {
            Console.WriteLine($"Resizing: {WorkingImageWidth}x{WorkingImageHeight}px -> {AtlasImageWidth}x{AtlasImageHeight}px");
            AtlasImage = AtlasImage.Resize(new Size(AtlasImageWidth, AtlasImageHeight), 0, 0, InterpolationFlags.Area);
        }

        // 出力
        String Date = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        String ResultFileName = "Result_" + Date + "_" + AtlasImageWidth + "x" + AtlasImageHeight + "." + ExportFileType;
        Console.WriteLine($"Export File: {ResultFileName}");
        String ExportDir = Directory.GetCurrentDirectory() + "\\Export";
        // Exportフォルダがなかったら、新規作成
        if (!Directory.Exists(ExportDir)) {
            Directory.CreateDirectory(ExportDir);
            Console.WriteLine("[Notice] Export Folder is Not Found. So this Program created the Folder.");
            Console.WriteLine($"\tDirectory Path: {ExportDir}");
        }
        // jpegなら
        if (Regex.IsMatch(ExportFileType, "(jpeg|jpg)")) {
            AtlasImage.SaveImage(ExportDir + "\\" + ResultFileName, new ImageEncodingParam(ImwriteFlags.JpegQuality, ExportQuality));
        }
        // pngなら
        if (Regex.IsMatch(ExportFileType, "png")) {
            AtlasImage.SaveImage(ExportDir + "\\" + ResultFileName, new ImageEncodingParam(ImwriteFlags.PngCompression, ExportQuality));
        }
        // webpなら
        if (Regex.IsMatch(ExportFileType, "webp")) {
            AtlasImage.SaveImage(ExportDir + "\\" + ResultFileName, new ImageEncodingParam(ImwriteFlags.WebPQuality, ExportQuality));
        }

        // 終了
        Console.WriteLine("Finished.");
        return 0;
    }

    // 最大公約数
    public static int Lcm(int a, int b) {
        return (int)(a * b / Gcd(a, b));
    }

    // 最小公倍数
    public static int Gcd(int a, int b) {
        if (a < b) {
            // 変数入れ替え
            (a, b) = (b, a);
        }
        // ユークリッドの互除法
        while (b != 0) {
            (a, b) = (b, a % b);
        }
        return a;
    }
}
