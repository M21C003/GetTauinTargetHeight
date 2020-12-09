using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;///ファイルを開くため
using System.Text.RegularExpressions;
using System.IO;///ファイルを開くため
using System.Data;
using System.Windows.Navigation;
using System.Windows.Media.Animation;

namespace GetTauinTargetHeight
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            SelectSaveData.Items.Add("MMtoRad.");
            SelectSaveData.Items.Add("PeekData");
            SelectSaveData.Items.Add("FFO");

            Button_SaveFile.IsEnabled = false;

            ProgressBlock.Text = ProgressBar.Value.ToString() + "%";
        }

        char charSeparator = ' ';

        string strLine = "";
        string[] arrCells;

        double dblLengthOfModelLegToDispPoint = new double();

        int intIdNum;

        Encoding encoding = Encoding.GetEncoding("Shift_JIS");

        DataTable DataTable1 = new DataTable();
        DataTable PositiveTau = new DataTable();
        DataTable NegativeTau = new DataTable();

        private void LoadFile_Click(object sender, RoutedEventArgs e)///ファイルを開く処理
        {
            OpenFileDialog fileDialog = new OpenFileDialog() { Title = "Open File", Filter = "FFG File(*.ffg)|*.ffg", RestoreDirectory = true };
            if (fileDialog.ShowDialog() == true)
            {
                TBoxFilePath.Text = fileDialog.SafeFileName.Replace(".ffg", "");

                ProgressBar.Value = 0;

                LoadFile(fileDialog.FileName);
                Button_SaveFile.IsEnabled = true;

                if ((bool)CB_RoadFFO.IsChecked)
                {
                    LoadFFO(fileDialog.FileName.Replace(".ffg", ".ffo"));
                }
            }
        }


        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog() { Title = "Save File", Filter = "CSV File(*.csv)|*.csv", RestoreDirectory = true, FileName = TBoxFilePath.Text };

            if (saveFileDialog.ShowDialog(this) == true)
            {
                StreamWriter writer = new StreamWriter(saveFileDialog.FileName, false, encoding);
                writer.WriteLine(StringBuildertoSelectSaveFiles(saveFileDialog.FileName));
                writer.Close();
            }
        }

        List<double> listTotalStepCount_inFFG = new List<double>();
        List<double> listDisp_inFFG = new List<double>();
        List<double> listShearForce_inFFG = new List<double>();

        /// <summary>
        /// FINALデータをRadians系に変換する関数。ついでに最大耐力とその変形角，ステップ数を記憶する。
        /// </summary>
        private void LoadFile(string filepath)
        {
            StreamReader objReader1 = new StreamReader(filepath, encoding);
            List<double> listCellsInFFG = new List<double>();

            listTotalStepCount_inFFG = new List<double>();
            listDisp_inFFG = new List<double>();
            listShearForce_inFFG = new List<double>();

            double StepCount = 1;
            dblLengthOfModelLegToDispPoint = double.Parse(TBoxDisplacementpoint.Text.ToString());

            while (objReader1.Peek() > -1) //データテーブルのカラム名とテーブルサイズを設定
            {
                strLine = objReader1.ReadLine();
                arrCells = strLine.Split(charSeparator);
                intIdNum = 0;

                for (int i = 0; i < arrCells.Length; i++)
                {
                    bool detectNum = double.TryParse(arrCells[i], out double result);
                    if (detectNum & !string.IsNullOrWhiteSpace(arrCells[i]))
                    {
                        listCellsInFFG.Add(double.Parse(arrCells[i].ToString()));
                        intIdNum = 1;
                    }
                }

                bool isMatchDistinctNum = Regex.IsMatch(intIdNum.ToString(), "1");
                if (isMatchDistinctNum)
                {
                    bool isMatchEndOfDisp = Regex.IsMatch(listCellsInFFG[0].ToString(), "-999");
                    bool isMatchEndOfShearF = Regex.IsMatch(listCellsInFFG[1].ToString(), "-999");
                    if (isMatchEndOfDisp & isMatchEndOfShearF)
                    {
                        break;
                    }

                    listTotalStepCount_inFFG.Add(StepCount);
                    listDisp_inFFG.Add(listCellsInFFG[0]/ dblLengthOfModelLegToDispPoint*1000);
                    listShearForce_inFFG.Add(listCellsInFFG[1]);

                    listCellsInFFG = new List<double>();
                    StepCount += 1;
                }
            }
            return;
        }

        private async void LoadFFO(string filepath)
        {
            GetModelData(filepath);
            ProgressBar.Value = 0;
            await GetOutStepData(filepath);
            this.dataGrid1.DataContext = DataTable1;
            await GetShear1(DataTable1);
            this.dataGrid2.DataContext = PositiveTau;
            this.dataGrid3.DataContext = NegativeTau;
            return;
        }

        int intStepCount = 0;
        double dblProgressStep;
        List<double> listCellswithoutNull;

        List<double> listTargetElement;//特定高さの要素番号
        List<double> listAllNode; //全節点番号
        List<double> listAxisXofAllNode; //全節点番号に対応するX座標
        List<double> listAxisYofAllNode; //全節点番号に対応するY座標
        List<double> listTargetNode; //特定高さの節点番号
        List<double> listAxisXofTargetNode; //特定高さの節点番号に対応するX座標

        List<double> listAllElement; //全要素番号
        List<double> listTypeofAllElement; //全要素番号に対応する要素タイプ番号
        List<double> listTypeofElement; //すべての四辺形要素のタイプ番号
        List<double> listThicknessofElementType;  //すべての四辺形要素のタイプ番号に対応する要素厚さ情報

        List<double> listTypeofTargetElement; //特定高さの要素番号に対応する要素タイプ番号
        List<double> listLengthofTargetElement; //特定高さの要素番号に対応するX方向長さ

        List<double> listThicknessofTargetElement;

        List<double> listAxisGX;
        List<double> listAxisGY;

        List<double> listSkipNodeNum; //節点番号が飛び番になったときの節点番号
        List<double> listAxisXOfSkipNode; //飛び番になったときの節点番号のX座標
        List<double> listAxisYOfSkipNode; //飛び番になったときの節点番号のY座標
        List<double> listFormerSkipNodeNum; //節点番号が飛び番になったときの直前の節点番号
        List<double> listAxisXOfFormerSkipNode; //飛び番になったときの直前の節点番号のX座標
        List<double> listAxisYOfFormerSkipNode; //飛び番になったときの直前の節点番号のY座標
        List<double> listAxisXForDrowingOP; //飛び番のX座標，重複無
        List<double> listHeaderNum; //百の桁の数字。i番目とi-1番目の差が1より大きい場合には異なる開口と判定する。

        List<double> listAxisYofNodeNumwithoutStab;

        double dblHeightofLowerStab = new double();
        double dblLengthofLowerStab = new double();

        /// <summary>
        /// モデルの節点，要素情報を読み込む関数
        /// </summary>
        /// <param name="filepath"></param>
        private void GetModelData(string filepath)
        {
            StreamReader objReader1 = new StreamReader(filepath, encoding);
            ProgressShow.IsIndeterminate = true;
            string strSearchWord;

            int intElmentHeight = int.Parse(TBoxTargetElementHeight.Text.ToString());
            intIdNum = 0;
            intStepCount = 0;

            listAllNode = new List<double>();
            listAxisXofAllNode = new List<double>();
            listAxisYofAllNode = new List<double>();
            listTargetNode = new List<double>();
            listAxisXofTargetNode = new List<double>();
            listAllElement = new List<double>();
            listTypeofAllElement = new List<double>();
            listTargetElement = new List<double>();
            listLengthofTargetElement = new List<double>();
            listTypeofTargetElement = new List<double>();
            listTypeofElement = new List<double>();
            listThicknessofElementType = new List<double>();
            listThicknessofTargetElement = new List<double>();

            listSkipNodeNum = new List<double>();
            listAxisXOfSkipNode = new List<double>();
            listAxisYOfSkipNode = new List<double>();
            listFormerSkipNodeNum = new List<double>();
            listAxisXOfFormerSkipNode = new List<double>();
            listAxisYOfFormerSkipNode = new List<double>();
            listAxisXForDrowingOP = new List<double>();

            listHeaderNum = new List<double>();

            listAxisYofNodeNumwithoutStab = new List<double>();

            while (objReader1.Peek() > -1)
            {
                strLine = objReader1.ReadLine();
                listCellswithoutNull = new List<double>();

                bool isMatchDistinctNum = Regex.IsMatch(intIdNum.ToString(), "0"); //節点データ頭を探す
                if (isMatchDistinctNum)
                {
                    strSearchWord = "   N O D E   D E F I N I T I O N";
                    intIdNum = GetNewId(strSearchWord, strLine, intIdNum, charSeparator);
                    continue;
                }

                isMatchDistinctNum = Regex.IsMatch(intIdNum.ToString(), "1"); //節点データ終わりまで指定の高さの節点を拾う
                if (isMatchDistinctNum)
                {
                    listCellswithoutNull = GetListwithoutNull(strLine, charSeparator).ToList();

                    strSearchWord = "   Q U A D R I L A T E R A L   E L E M E N T S";
                    intIdNum = GetNewId(strSearchWord, strLine, intIdNum, charSeparator);

                    if (listCellswithoutNull.Count == 0) //とりあえず読み込んだ行に節点番号が存在すれば，AllNodeにデータを突っ込む
                    {
                        continue;
                    }
                    else if (listCellswithoutNull[0].ToString().Length < 5) //要素番号10000(5桁)以上は読み込まない。。。

                    {
                        listAllNode.Add(listCellswithoutNull[0]);
                        listAxisXofAllNode.Add(listCellswithoutNull[1]);
                        listAxisYofAllNode.Add(listCellswithoutNull[2]);

                        if (listAllNode.Count > 1) //このif文では百の位が前の節点と同じで，前の節点との差が1より大きい場合に開口が存在するものとしてリストに追加。
                        {
                            double dblNodeNum = listAllNode[listAllNode.Count - 1];
                            double dblNodeNumXXXi = dblNodeNum % 10;
                            double dblNodeNumXX = Math.Floor(dblNodeNum / 100);
                            double dblNodeNumXiXX = dblNodeNumXX % 10;

                            double dblFormerNodeNum = listAllNode[listAllNode.Count - 2];
                            double dblFormerNodeNumXXXi = dblFormerNodeNum % 10;
                            double dblFormerNodeNumXX = Math.Floor(dblFormerNodeNum / 100);
                            double dblFormerNodeNumXiXX = dblFormerNodeNumXX % 10;

                            ///kaitemasu
                            if (dblNodeNumXXXi == 9 & dblNodeNumXiXX != dblFormerNodeNumXiXX)
                            {
                                if (listAxisYofNodeNumwithoutStab.Count == 0)
                                {
                                    dblHeightofLowerStab = listCellswithoutNull[2];
                                    dblLengthofLowerStab = listCellswithoutNull[1];
                                }
                                listAxisYofNodeNumwithoutStab.Add(listCellswithoutNull[2]);
                            }

                            if (listAllNode[listAllNode.Count - 1] - listAllNode[listAllNode.Count - 2] > 1 & dblNodeNumXiXX == dblFormerNodeNumXiXX)
                            {
                                if (listAxisXOfSkipNode.Count == 0)
                                {
                                    listAxisXForDrowingOP.Add(listAxisXofAllNode[listAxisXofAllNode.Count - 1]);
                                }
                                else if (!listAxisXForDrowingOP.Contains(listAxisXOfSkipNode[listAxisXOfSkipNode.Count - 1]))
                                {
                                    listAxisXForDrowingOP.Add(listAxisXOfSkipNode[listAxisXOfSkipNode.Count - 1]);
                                }

                                listHeaderNum.Add(Convert.ToInt32(Math.Floor(listAllNode[listAllNode.Count - 1] / 100)));
                                listFormerSkipNodeNum.Add(listAllNode[listAllNode.Count - 2]);
                                listSkipNodeNum.Add(listAllNode[listAllNode.Count - 1]);
                                listAxisXOfFormerSkipNode.Add(listAxisXofAllNode[listAxisXofAllNode.Count - 2]);
                                listAxisXOfSkipNode.Add(listAxisXofAllNode[listAxisXofAllNode.Count - 1]);
                                listAxisYOfFormerSkipNode.Add(listAxisYofAllNode[listAxisYofAllNode.Count - 2]);
                                listAxisYOfSkipNode.Add(listAxisYofAllNode[listAxisYofAllNode.Count - 1]);
                            }
                        }
                    }

                    if (intElmentHeight == int.Parse(listCellswithoutNull[2].ToString()))
                    {
                        listTargetNode.Add(listCellswithoutNull[0]);
                        listAxisXofTargetNode.Add(listCellswithoutNull[1]);
                    }
                    continue;
                }

                isMatchDistinctNum = Regex.IsMatch(intIdNum.ToString(), "2");///四辺形要素データ終わりまで読み込んで，指定の高さの要素を探す。
                if (isMatchDistinctNum)
                {
                    listCellswithoutNull = GetListwithoutNull(strLine, charSeparator).ToList();

                    strSearchWord = "   B E A M   E L E M E N T S";
                    intIdNum = GetNewId(strSearchWord, strLine, intIdNum, charSeparator);

                    if (listCellswithoutNull.Count == 0)
                    {
                        continue;
                    }
                    else
                    {
                        listAllElement.Add(listCellswithoutNull[0]);
                        listTypeofAllElement.Add(listCellswithoutNull[5]);
                    }

                    if (listTargetNode.Contains(double.Parse(listCellswithoutNull[1].ToString())))
                    {
                        listLengthofTargetElement.Add(Math.Abs(listAxisXofTargetNode[listTargetNode.IndexOf(listCellswithoutNull[2])] - listAxisXofTargetNode[listTargetNode.IndexOf(listCellswithoutNull[1])]));
                        listTargetElement.Add(listCellswithoutNull[0]);
                        listTypeofTargetElement.Add(listCellswithoutNull[5]);
                    }
                    continue;
                }

                isMatchDistinctNum = Regex.IsMatch(intIdNum.ToString(), "3");///要素データの開始位置を探す。
                if (isMatchDistinctNum)
                {
                    strSearchWord = "   Q U A D R I L A T E R A L   E L E M E N T   T Y P E";
                    intIdNum = GetNewId(strSearchWord, strLine, intIdNum, charSeparator);
                    continue;
                }

                isMatchDistinctNum = Regex.IsMatch(intIdNum.ToString(), "4");///要素データの厚さを読み込み，指定の高さの要素を探す。
                if (isMatchDistinctNum)
                {
                    if (strLine.Contains("THICKNESS"))
                    {
                        listCellswithoutNull = GetListwithoutNull(strLine, charSeparator).ToList();
                        listTypeofElement.Add(listCellswithoutNull[0]);
                        listThicknessofElementType.Add(listCellswithoutNull[2]);
                    }

                    strSearchWord = "   B E A M   E L E M E N T   T Y P E";
                    intIdNum = GetNewId(strSearchWord, strLine, intIdNum, charSeparator);
                    continue;
                }

                isMatchDistinctNum = Regex.IsMatch(intIdNum.ToString(), "5"); 
                if (isMatchDistinctNum)
                {
                    break;
                }
            }
            return;
        }

        /// <summary>
        /// FFOのうち，最大耐力発揮時の要素の各データ読み込み
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        private async Task GetOutStepData(string filepath)
        {
            StreamReader objReader1 = new StreamReader(filepath, encoding);
            ProgressShow.IsIndeterminate = true;

            int intElmentHeight = int.Parse(TBoxTargetElementHeight.Text.ToString());
            intIdNum = 0;
            intStepCount = 0;

            List<double> listTmpData = new List<double>();

            DataTable1 = new DataTable();

            DataTable1.Columns.Add("StepNum");
            DataTable1.Columns.Add("Disp");
            DataTable1.Columns.Add("ShearForce");
            for (int i = 0; i < listTargetElement.Count ; i++) 
            {
                DataTable1.Columns.Add(listTargetElement[i].ToString());
            }

            DataRow InsertData = DataTable1.NewRow();

            DataRow InsertDataOfElementNum = DataTable1.NewRow();
            DataRow InsertDataOfElementType = DataTable1.NewRow();
            DataRow InsertDataOfThickness = DataTable1.NewRow();
            DataRow InsertDataOfLength = DataTable1.NewRow();

            for (int i = 0; i < listTargetElement.Count ; i++)
            {
                InsertDataOfElementNum[i + 3] = listTargetElement[i];
                InsertDataOfElementType[i+3] = listTypeofTargetElement[i];
                listThicknessofTargetElement.Add(listThicknessofElementType[int.Parse(listTypeofTargetElement[i].ToString())]);
                InsertDataOfThickness[i+3] = listThicknessofTargetElement[i];
                InsertDataOfLength[i+3] = listLengthofTargetElement[i];
            }

            DataTable1.Rows.Add(InsertDataOfElementNum);
            DataTable1.Rows.Add(InsertDataOfElementType);
            DataTable1.Rows.Add(InsertDataOfThickness);
            DataTable1.Rows.Add(InsertDataOfLength);

            await Task.Run((() =>
            {
                while (objReader1.Peek() > -1)
                {
                    strLine = objReader1.ReadLine();
                    listCellswithoutNull = new List<double>();

                    bool isMatchDistinctNum = Regex.IsMatch(intIdNum.ToString(), "0");///ステップデータを見つける
                    if (isMatchDistinctNum)
                    {
                        if (strLine.Contains(" ==========================================================< STEP NO."))
                        {
                            strLine = strLine.Replace("NO.", "");
                            listCellswithoutNull = GetListwithoutNull(strLine, charSeparator).ToList();

                            for (int i = 0; i < 12; i++)
                            {
                                strLine = objReader1.ReadLine();
                            }
                            intIdNum = 1;

                            listTmpData.Add(listCellswithoutNull[0]);
                            listTmpData.Add(listDisp_inFFG[int.Parse(listTmpData[0].ToString())]);
                            listTmpData.Add(listShearForce_inFFG[int.Parse(listTmpData[0].ToString())]);
                        }
                    }

                    isMatchDistinctNum = Regex.IsMatch(intIdNum.ToString(), "1");///探している要素番号のデータか確認。
                    if (isMatchDistinctNum)
                    {
                        if (strLine.Contains("    ELEMENT NO."))
                        {
                            strLine = strLine.Replace("NO.", "");
                            listCellswithoutNull = GetListwithoutNull(strLine, charSeparator).ToList();

                            if (listTargetElement.Contains(listCellswithoutNull[0]))
                            {
                                objReader1.ReadLine();
                                objReader1.ReadLine();

                                strLine = objReader1.ReadLine();

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    ProgressBar.Value = ProgressBar.Value + dblProgressStep;
                                });

                                listCellswithoutNull = GetListwithoutNull(strLine, charSeparator).ToList();
                                listTmpData.Add(listCellswithoutNull[5]);
                            }
                        }
                        else if (strLine.Contains("L O A D I N G    S T E P S"))
                        {
                            for (int i = 0; i < listTmpData.Count ; i++)
                            {
                                InsertData[i] = listTmpData[i];
                            }
                            DataTable1.Rows.Add(InsertData);
                            InsertData = DataTable1.NewRow();
                            listTmpData = new List<double>();
                            intIdNum = 0;
                        }
                    }
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProgressShow.IsIndeterminate = false;
                });
            }));
            if (ProgressBar.Value != 100)
            {
                ProgressBar.Value = Math.Ceiling(ProgressBar.Value);
            }
            return;
        }

         /// <summary>
        /// 読み込んだ行から数字のみを配列に追加して返す関数。ただし，行中NO.10001のように文字と数値がついている場合は，読み込む前にstrLineからReplaceでNO.を消すとよい。
        /// </summary>
        /// <param name="strLine"></param>
        /// <param name="charSeparator"></param>
        /// <returns></returns>
        private IEnumerable<double> GetListwithoutNull(string strLine, char charSeparator)
        {
            string[] arrCells;
            List<double> listTempData = new List<double>();
            arrCells = strLine.Split(charSeparator);

            for (int i = 0; i < arrCells.Length; i++)
            {
                bool isMatch_dbl = double.TryParse(arrCells[i].ToString(), out double dblCheck);
                if (isMatch_dbl & !string.IsNullOrWhiteSpace(arrCells[i]))
                {
                    listTempData.Add(double.Parse(arrCells[i].ToString()));
                }
            }

            IEnumerable<double> enumeCheckArray = listTempData;
            return enumeCheckArray;
        }

        /// <summary>
        /// 特定のワードが文中にあった時に異なる処理を行いたい場合に用いるintId=0→1,intId=1→2・・と返す
        /// </summary>
        /// <param name="strSearchWord"></param>
        /// <param name="strLine"></param>
        /// <param name="intIdNum"></param>
        /// <param name="charSeparator"></param>
        /// <returns></returns>
        private int GetNewId(string strSearchWord, string strLine, int intIdNum, char charSeparator)
        {
            int intNewIdNum;

            bool isMatchTargetWord = Regex.IsMatch(strLine.ToString(), strSearchWord);
            if (isMatchTargetWord)
            {
                intNewIdNum = intIdNum + 1;
            }
            else
            {
                intNewIdNum = intIdNum;
            }
            return intNewIdNum;
        }
        
        int intColumnsNumInTotal = 0;
        private async Task GetShear1(DataTable dataTable) 
        {
            List<double> listStepData = new List<double>();
            List<double> listDispData = new List<double>();
            List<double> listShearForceData = new List<double>();
            List<double> listSumTau = new List<double>();
            List<double> listTauinThisElement = new List<double>();

            List<int> listPositiveorNegative = new List<int>();///正負交番で0,0,1,1,2,2,3,3,4,4,,,,
            int intCountPositive = 0;
            int intCountNegative = 0;
            int intColumnsCountOfNegativeDisp = 0;

            double dblSumOfLength=new double();

            DataTable tmpDataTableP = new DataTable();
            DataTable tmpDataTableN = new DataTable();

            await Task.Run((() =>
            {
                for (int i = 3; i < dataTable.Columns.Count; i++) 
                {
                    if (i == 3)///データの内，ステップ数・変位・せん断力を処理する
                    {
                        for (int j = 5; j < dataTable.Rows.Count; j++)///ステップ数・変位・せん断力をコピーするのは共通作業
                        {
                            listStepData.Add(double.Parse(dataTable.Rows[j][i - 3].ToString()));
                            listDispData.Add(double.Parse(dataTable.Rows[j][i - 2].ToString()));
                            listShearForceData.Add(double.Parse(dataTable.Rows[j][i - 1].ToString()));

                            if (listDispData[j-5] > 0 & double.Parse(dataTable.Rows[j][i].ToString()) >= 0)///変位が正→せん断応力度正値を無視
                            {
                                listSumTau.Add(0);
                            }
                            else if (listDispData[j-5] > 0 & double.Parse(dataTable.Rows[j][i].ToString()) < 0)
                            {
                                listSumTau.Add(-1 * listLengthofTargetElement[i - 3] * listThicknessofTargetElement[i - 3] * double.Parse(dataTable.Rows[j][i].ToString()));
                            }
                            else if (listDispData[j-5] < 0 & double.Parse(dataTable.Rows[j][i].ToString()) <= 0)
                            {
                                listSumTau.Add(0);
                            }
                            else if (listDispData[j-5] < 0 & double.Parse(dataTable.Rows[j][i].ToString()) > 0)
                            {
                                listSumTau.Add(listLengthofTargetElement[i - 3] * listThicknessofTargetElement[i - 3] * double.Parse(dataTable.Rows[j][i].ToString()));
                            }
                        }

                        if (intColumnsNumInTotal != 0)///すでにデータテーブルに存在する場合，データをコピーする
                        {
                            intColumnsNumInTotal = PositiveTau.Columns.Count;

                            tmpDataTableP = PositiveTau.Copy();
                            tmpDataTableN = NegativeTau.Copy();
                            PositiveTau = new DataTable();
                            NegativeTau = new DataTable();

                            for (int j = 0; j < intColumnsNumInTotal + 3; j++)//+3はステップ数・変位・せん断力の分のカラム
                            {
                                PositiveTau.Columns.Add(j.ToString());
                                NegativeTau.Columns.Add(j.ToString());
                            }

                            for (int j = 0; j < listDispData.Count; j++) //正負交番で0,0,1,1,2,2,3,3,4,4,,,, listDispData[i=0;i++]行のDataTableに追加するために必要
                            {
                                if (listDispData[j] > 0)
                                {
                                    listPositiveorNegative.Add(intCountPositive);
                                    intCountPositive += 1;
                                    if (intCountPositive > tmpDataTableP.Rows.Count) 
                                    {
                                        PositiveTau.Rows.Add();
                                    }
                                }
                                else
                                {
                                    listPositiveorNegative.Add(intCountNegative);
                                    intCountNegative += 1;
                                    if (intCountNegative > tmpDataTableN.Rows.Count)
                                    {
                                        NegativeTau.Rows.Add();
                                    }
                                }
                            }

                            for (int j = 0; j < listDispData.Count; j++)
                            {
                                DataRow drTmpP = PositiveTau.NewRow();
                                DataRow drTmpN = NegativeTau.NewRow();

                                if (listDispData[j] > 0)
                                {
                                    for (int k = 0; k < tmpDataTableP.Columns.Count; k++)
                                    {
                                        drTmpP[k] = tmpDataTableP.Rows[listPositiveorNegative[j]][k];
                                    }
                                    drTmpP[tmpDataTableP.Columns.Count] = "";
                                    drTmpP[tmpDataTableP.Columns.Count + 1] = "";
                                    drTmpP[tmpDataTableP.Columns.Count + 2] = "";
                                    PositiveTau.Rows.Add(drTmpP);
                                }
                                else
                                {
                                    for (int k = 0; k < tmpDataTableP.Columns.Count; k++)
                                    {
                                        drTmpN[k] = tmpDataTableN.Rows[listPositiveorNegative[j]][k];
                                    }
                                    drTmpN[tmpDataTableN.Columns.Count] = "";
                                    drTmpN[tmpDataTableN.Columns.Count + 1] = "";
                                    drTmpN[tmpDataTableN.Columns.Count + 2] = "";
                                    NegativeTau.Rows.Add(drTmpN);
                                }
                            }
                        }
                        else ///データテーブルに存在しない場合
                        {
                            for (int j = 0; j < intColumnsNumInTotal + 3; j++)
                            {
                                PositiveTau.Columns.Add(j.ToString());
                                NegativeTau.Columns.Add(j.ToString());
                            }

                            for (int j = 0; j < listDispData.Count; j++)
                            {
                                if (listDispData[j] > 0)
                                {
                                    listPositiveorNegative.Add(intCountPositive);
                                    intCountPositive += 1;
                                    PositiveTau.Rows.Add();
                                }
                                else 
                                {
                                    listPositiveorNegative.Add(intCountNegative);
                                    intCountNegative += 1;
                                    NegativeTau.Rows.Add();
                                }
                            }
                        }

                        for (int j = 0; j < listStepData.Count; j++)
                        {
                            if (listDispData[j] > 0)
                            {
                                PositiveTau.Rows[listPositiveorNegative[j]][intColumnsNumInTotal] = listStepData[j];
                                PositiveTau.Rows[listPositiveorNegative[j]][intColumnsNumInTotal + 1] = listDispData[j];
                                PositiveTau.Rows[listPositiveorNegative[j]][intColumnsNumInTotal + 2] = listShearForceData[j];
                            }
                            else
                            {
                                NegativeTau.Rows[listPositiveorNegative[j]][intColumnsNumInTotal] = listStepData[j];
                                NegativeTau.Rows[listPositiveorNegative[j]][intColumnsNumInTotal + 1] = listDispData[j];
                                NegativeTau.Rows[listPositiveorNegative[j]][intColumnsNumInTotal + 2] = listShearForceData[j];
                            }
                        }

                        intColumnsCountOfNegativeDisp = intColumnsNumInTotal+1;
                        intColumnsNumInTotal += 3;
                    }
                    else
                    {
                        for (int j = 5; j < dataTable.Rows.Count; j++)///
                        {

                            if (listDispData[j - 5] > 0 & double.Parse(dataTable.Rows[j][i].ToString()) >= 0)///変位が正→せん断応力度正値を無視
                            {
                                listTauinThisElement.Add(0);
                            }
                            else if (listDispData[j - 5] > 0 & double.Parse(dataTable.Rows[j][i].ToString()) < 0)
                            {
                                listTauinThisElement.Add(-1 * listLengthofTargetElement[i - 3] * listThicknessofTargetElement[i - 3] * double.Parse(dataTable.Rows[j][i].ToString()));
                            }
                            else if (listDispData[j - 5] < 0 & double.Parse(dataTable.Rows[j][i].ToString()) <= 0)
                            {
                                listTauinThisElement.Add(0);
                            }
                            else if (listDispData[j - 5] < 0 & double.Parse(dataTable.Rows[j][i].ToString()) > 0)
                            {
                                listTauinThisElement.Add(listLengthofTargetElement[i - 3] * listThicknessofTargetElement[i - 3] * double.Parse(dataTable.Rows[j][i].ToString()));
                            }
                        }

                        if ((double.Parse(dataTable.Rows[2][i].ToString()) != 200 & dblTypeOfFormerElement != double.Parse(dataTable.Rows[1][i].ToString()) & dblThicknessOfFormerElement != 200))///壁板要素(thickness=80→thickness=!200)のうち，要素番号が変わったらメッセージ
                        {
                            var result = MessageBox.Show($"要素番号が変化しました。現在と異なるグループですか？" +
                                $"\r\n要素番号{dblNumOfFormerElement}→{double.Parse(dataTable.Rows[0][i].ToString())}" +
                                $"\r\n要素タイプ番号{dblTypeOfFormerElement}→{double.Parse(dataTable.Rows[1][i].ToString())}" +
                                $"\r\n要素厚さ{dblThicknessOfFormerElement}→{double.Parse(dataTable.Rows[2][i].ToString())}", "Notify", MessageBoxButton.YesNo);
                            if (result == MessageBoxResult.Yes)///つまり，列の追加と書き込み，リセットが必要
                            {
                                tmpDataTableP = PositiveTau.Copy();
                                tmpDataTableN = NegativeTau.Copy();
                                PositiveTau = new DataTable();
                                NegativeTau = new DataTable();

                                for (int j = 0; j < intColumnsNumInTotal + 1; j++)
                                {
                                    PositiveTau.Columns.Add(j.ToString());
                                    NegativeTau.Columns.Add(j.ToString());
                                }

                                for (int j = 0; j < listDispData.Count; j++)
                                {
                                    DataRow drTmpP = PositiveTau.NewRow();
                                    DataRow drTmpN = NegativeTau.NewRow();

                                    if (listDispData[j] > 0)
                                    {
                                        for (int k = 0; k < tmpDataTableP.Columns.Count; k++)
                                        {
                                            drTmpP[k] = tmpDataTableP.Rows[listPositiveorNegative[j]][k];
                                        }

                                        drTmpP[tmpDataTableP.Columns.Count] = listSumTau[j]/ dblSumOfLength;
                                        listSumTau[j] = 0;
                                        PositiveTau.Rows.Add(drTmpP);
                                    }
                                    else
                                    {
                                        for (int k = 0; k < tmpDataTableN.Columns.Count; k++)
                                        {
                                            drTmpN[k] = tmpDataTableN.Rows[listPositiveorNegative[j]][k];
                                        }

                                        drTmpN[tmpDataTableN.Columns.Count] = listSumTau[j] / dblSumOfLength;
                                        listSumTau[j] = 0;
                                        NegativeTau.Rows.Add(drTmpN);
                                    }
                                }
                                dblSumOfLength = 0;
                                intColumnsNumInTotal += 1;
                            }
                        }
                        else if (dblThicknessOfFormerElement != double.Parse(dataTable.Rows[2][i].ToString()) || (double.Parse(dataTable.Rows[2][i].ToString()) != 200 & dblNumOfFormerElement - double.Parse(dataTable.Rows[0][i].ToString()) + 1 < 0))
                        {
                            tmpDataTableP = PositiveTau.Copy();
                            tmpDataTableN = NegativeTau.Copy();
                            PositiveTau = new DataTable();
                            NegativeTau = new DataTable();

                            for (int j = 0; j < intColumnsNumInTotal + 1; j++)
                            {
                                PositiveTau.Columns.Add(j.ToString());
                                NegativeTau.Columns.Add(j.ToString());
                            }

                            for (int j = 0; j < listDispData.Count; j++)
                            {
                                DataRow drTmpP = PositiveTau.NewRow();
                                DataRow drTmpN = NegativeTau.NewRow();

                                if (listDispData[j] > 0)
                                {
                                    for (int k = 0; k < tmpDataTableP.Columns.Count; k++)
                                    {
                                        drTmpP[k] = tmpDataTableP.Rows[listPositiveorNegative[j]][k];
                                    }

                                    drTmpP[tmpDataTableP.Columns.Count] = listSumTau[j]/ dblSumOfLength;
                                    listSumTau[j] = 0;
                                    PositiveTau.Rows.Add(drTmpP);
                                }
                                else
                                {
                                    for (int k = 0; k < tmpDataTableP.Columns.Count; k++)
                                    {
                                        drTmpN[k] = tmpDataTableN.Rows[listPositiveorNegative[j]][k];
                                    }

                                    drTmpN[tmpDataTableN.Columns.Count] = listSumTau[j]/ dblSumOfLength;
                                    listSumTau[j] = 0;
                                    NegativeTau.Rows.Add(drTmpN);
                                }
                            }
                            dblSumOfLength = 0;
                            intColumnsNumInTotal += 1;
                        }

                        for (int j = 0; j < listSumTau.Count; j++)/// 共通の作業
                        {
                            listSumTau[j] = listSumTau[j] + listTauinThisElement[j];
                        }

                        listTauinThisElement = new List<double>();

                    }

                    dblSumOfLength += listLengthofTargetElement[i - 3];
                    dblNumOfFormerElement = double.Parse(dataTable.Rows[0][i].ToString());
                    dblTypeOfFormerElement = double.Parse(dataTable.Rows[1][i].ToString());
                    dblThicknessOfFormerElement = double.Parse(dataTable.Rows[2][i].ToString());
                }

                tmpDataTableP = PositiveTau.Copy();
                tmpDataTableN = NegativeTau.Copy();
                PositiveTau = new DataTable();
                NegativeTau = new DataTable();

                for (int j = 0; j < intColumnsNumInTotal + 1; j++)
                {
                    PositiveTau.Columns.Add(j.ToString());
                    NegativeTau.Columns.Add(j.ToString());
                }

                for (int j = 0; j < listDispData.Count; j++)
                {
                    DataRow drTmpP = PositiveTau.NewRow();
                    DataRow drTmpN = NegativeTau.NewRow();

                    if (listDispData[j] > 0)
                    {
                        for (int k = 0; k < tmpDataTableP.Columns.Count; k++)
                        {
                            drTmpP[k] = tmpDataTableP.Rows[listPositiveorNegative[j]][k];
                        }

                        drTmpP[tmpDataTableP.Columns.Count] = listSumTau[j]/ dblSumOfLength;
                        PositiveTau.Rows.Add(drTmpP);
                    }
                    else
                    {
                        for (int k = 0; k < tmpDataTableN.Columns.Count; k++)
                        {
                            drTmpN[k] = tmpDataTableN.Rows[listPositiveorNegative[j]][k];
                        }

                        drTmpN[tmpDataTableN.Columns.Count] = listSumTau[j]/ dblSumOfLength;
                        NegativeTau.Rows.Add(drTmpN);
                    }
                }
                intColumnsNumInTotal += 1;

                for (int i = 0; i < NegativeTau.Rows.Count; i++) 
                {
                    NegativeTau.Rows[i][intColumnsCountOfNegativeDisp] = -1 * double.Parse(NegativeTau.Rows[i][intColumnsCountOfNegativeDisp].ToString());
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProgressShow.IsIndeterminate = false;
                });
            }));
        }

        string strLoadingStep;

        double dblNumOfFormerElement;
        double dblTypeOfFormerElement;
        double dblThicknessOfFormerElement;

        /// <summary>
        ///保存時実行呼び出し関数
        /// </summary>
        private StringBuilder StringBuildertoSelectSaveFiles(string filepath)
        {
            StringBuilder StringBuildertoSaveFiles = new StringBuilder();
            DataTable SaveDataTable = new DataTable();

            if (SelectSaveData.SelectedIndex == 0)
            {
                SaveDataTable = DataTable1.Copy();
            }
            else if (SelectSaveData.SelectedIndex == 1)
            {
                SaveDataTable = PositiveTau.Copy();
            }
            else if (SelectSaveData.SelectedIndex == 2)
            {
                SaveDataTable = NegativeTau.Copy();
            }

            for (int i = 0; i < SaveDataTable.Columns.Count; i++)
            {
                StringBuildertoSaveFiles.Append(SaveDataTable.Columns[i].ColumnName.ToString() + ',');
            }
            StringBuildertoSaveFiles.Append("\r\n");

            for (int i = 0; i < SaveDataTable.Rows.Count; i++)
            {
                for (int j = 0; j < SaveDataTable.Columns.Count; j++)
                {
                    StringBuildertoSaveFiles.Append(SaveDataTable.Rows[i][j].ToString() + ',');
                }
                StringBuildertoSaveFiles.Append("\r\n");
            }
            return StringBuildertoSaveFiles;
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            bool IsMatchNumber = Regex.IsMatch(e.Text, "^[0-9.]");
            if (!IsMatchNumber)
            {
                e.Handled = true;
            }
        }
        private void StringValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("^[0-9]*\\.?[0-9]+$");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ProgressBlock.Text = ProgressBar.Value.ToString() + "%";
        }

        private void RoadFFO_Checked(object sender, RoutedEventArgs e)
        {
            CB_ShearForce.IsEnabled = true;
        }

        private void RoadFFO_Unchecked(object sender, RoutedEventArgs e)
        {
            CB_ShearForce.IsEnabled = false;
        }
    }
}
