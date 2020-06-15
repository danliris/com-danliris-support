﻿using com.danliris.support.lib.Helpers;
using com.danliris.support.lib.ViewModel;
using Com.Moonlay.NetCore.Lib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text;

namespace com.danliris.support.lib.Services
{
    public class FactItemMutationService
    {
        SupportDbContext context;
        public FactItemMutationService(SupportDbContext _context)
        {
            this.context = _context;
        }

        public IQueryable<FactMutationItemViewModel> GetUnitItemBBReport(int unit, DateTime? dateFrom, DateTime? dateTo, int offset)
        {
            DateTime d1 = dateFrom == null ? new DateTime(1970, 1, 1) : (DateTime)dateFrom;
            DateTime d2 = dateTo == null ? DateTime.Now : (DateTime)dateTo;


            string DateFrom = d1.ToString("yyyy-MM-dd");
            string DateTo = d2.ToString("yyyy-MM-dd");
            List<FactMutationItemViewModel> reportData = new List<FactMutationItemViewModel>();
            try
            {
                string connectionString = APIEndpoint.ConnectionString;
                using (SqlConnection conn =
                    new SqlConnection(connectionString))
                {
                    string unitCode = "declare @unitCode int= " + unit + " ";
                    if (unit == 0)
                    {
                        unitCode = "declare @unitCode  int =" + SqlInt32.Null + " ";
                    }
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(
                        "declare @StartDate datetime = '" + DateFrom + "' " +
                        "declare @EndDate datetime = '" + DateTo + "' " +
                         unitCode +
                        "declare @balanceDate datetime = (select top(1) d.CreateDate from BalanceStock_Temp d join Stock_Temp s on d.StockId = s.StockId where @unitCode is null or s.UnitCode = @unitCode  order by d.CreateDate desc) "+
                        "select data.unitCode,data.ItemCode,ItemName, UnitQtyName, Convert(float, SUM(BeginQty)) as BeginQty, Convert(float, SUM(ReceiptQty)) ReceiptQty, Convert(float, SUM(ExpenditureQty)) ExpenditureQty, Convert(float, SUM(AdjustmentQty)) AdjustmentQty, Convert(float, SUM(OpnameQty)) as OpnameQty into #balance from( "+
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where @unitCode is null or UnitCode = @unitCode and TYPE = 'Balance' and[ClassificationCode] = 'BB'and Date = @balanceDate "+
                        "union all "+
                        "select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where @unitCode is null or UnitCode = @unitCode and TYPE = 'receipt' and[ClassificationCode] = 'BB' and(DATE > @balanceDate and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode "+
                        "union all "+
                        "select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where @unitCode is null or UnitCode = @unitCode and TYPE = 'expenditure' and[ClassificationCode] = 'BB' and(DATE > @balanceDate and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode "+
                        ")as data "+
                        "group by ItemCode, ItemName,UnitQtyName,unitCode "+ 
                        "select data.unitCode,data.ItemCode,ItemName, UnitQtyName,round(SUM(BeginQty), 2) as BeginQty,SUM(ReceiptQty) ReceiptQty, SUM(ExpenditureQty)ExpenditureQty,SUM(AdjustmentQty) AdjustmentQty, SUM(OpnameQty) as OpnameQty from( "+
                        "select * from #balance "+
                        "union all "+
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where @unitCode is null or UnitCode = @unitCode and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode "+
                        "union all "+
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where @unitCode is null or UnitCode = @unitCode and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode) as data "+
                        "group by itemcode,itemname, unitqtyname,unitCode "+
                        "order by itemcode "+
                        "drop table #balance", conn))
                    {
                        SqlDataAdapter dataAdapter = new SqlDataAdapter(cmd);
                        DataSet dSet = new DataSet();
                        dataAdapter.Fill(dSet);
                        foreach (DataRow data in dSet.Tables[0].Rows)
                        {
                            FactMutationItemViewModel view = new FactMutationItemViewModel
                            {
                                unitCode = data["unitCode"].ToString(),
                                ItemCode = data["ItemCode"].ToString(),
                                ItemName = data["ItemName"].ToString(),
                                UnitQtyName = data["UnitQtyName"].ToString(),
                                BeginQty = String.Format("{0:n}", (double)data["BeginQty"]),
                                ReceiptQty = String.Format("{0:n}", (double)data["ReceiptQty"]),
                                ExpenditureQty = String.Format("{0:n}", (double)data["ExpenditureQty"]),
                                AdjustmentQty = String.Format("{0:n}", (double)data["AdjustmentQty"]),
                                OpnameQty = String.Format("{0:n}", (double)data["OpnameQty"]),
                                LastQty = String.Format("{0:n}", ((double)data["BeginQty"] + (double)data["ReceiptQty"] - (double)data["ExpenditureQty"] + (double)data["AdjustmentQty"] + (double)data["OpnameQty"])),
                                Diff= String.Format("{0:n}",0)
                            };
                            reportData.Add(view);
                        }
                    }
                    conn.Close();
                }
            }
           catch (SqlException ex)
            {
                //Log exception
                //Display Error message
            }


            return reportData.AsQueryable();
        }

        public virtual Tuple<List<FactMutationItemViewModel>, int> GetReportBBUnit(int unit, DateTime? dateFrom, DateTime? dateTo, int page, int size, string Order, int offset)
        {
            var Query = GetUnitItemBBReport(unit, dateFrom, dateTo, offset);

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            if (OrderDictionary.Count.Equals(0))
            {
                Query = Query.OrderBy(b => b.ItemCode);
            }
            else
            {
                string Key = OrderDictionary.Keys.First();
                string OrderType = OrderDictionary[Key];

                //Query = Query.OrderBy(string.Concat(Key, " ", OrderType));
            }


            Pageable<FactMutationItemViewModel> pageable = new Pageable<FactMutationItemViewModel>(Query, page - 1, size);
            List<FactMutationItemViewModel> Data = pageable.Data.ToList<FactMutationItemViewModel>();

            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData);
        }

        public virtual MemoryStream GenerateExcelBBUnit(int unit, DateTime? dateFrom, DateTime? dateTo, int offset)
        {
            var Query = GetUnitItemBBReport(unit, dateFrom, dateTo, offset);
            Query = Query.OrderBy(b => b.ItemCode);
            DataTable result = new DataTable();
            result.Columns.Add(new DataColumn() { ColumnName = "Kode Barang", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Nama Barang", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Satuan", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Saldo Awal", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Pemasukan", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Pengeluaran", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Penyesuaian", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Saldo Akhir", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Stock Opname", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Selisih", DataType = typeof(Double) });
            if (Query.ToArray().Count() == 0)
                result.Rows.Add("", "", "", "", "", "", "", "", "", ""); // to allow column name to be generated properly for empty data as template
            else
                foreach (var item in Query)
                {
                    result.Rows.Add((item.ItemCode), item.ItemName, item.UnitQtyName, item.BeginQty, item.ReceiptQty, item.ExpenditureQty, item.AdjustmentQty, item.LastQty, item.OpnameQty, item.Diff);
                }
            return Excel.CreateExcel(new List<KeyValuePair<DataTable, string>>() { new KeyValuePair<DataTable, string>(result, "Territory") }, true);
        }

        public IQueryable<FactMutationItemViewModel> GetUnitItemBPReport(int unit, DateTime? dateFrom, DateTime? dateTo, int offset)
        {
            DateTime d1 = dateFrom == null ? new DateTime(1970, 1, 1) : (DateTime)dateFrom;
            DateTime d2 = dateTo == null ? DateTime.Now : (DateTime)dateTo;



            string DateFrom = d1.ToString("yyyy-MM-dd");
            string DateTo = d2.ToString("yyyy-MM-dd");
            List<FactMutationItemViewModel> reportData = new List<FactMutationItemViewModel>();
            try
            {
                string connectionString = APIEndpoint.ConnectionString;
                using (SqlConnection conn =
                    new SqlConnection(connectionString))
                {
                    string unitCode = "declare @unitCode int= " + unit + " ";
                    if (unit == 0)
                    {
                        unitCode = "declare @unitCode  int =" + SqlInt32.Null + " ";
                    }
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(
                        "declare @StartDate datetime = '" + DateFrom + "' " +
                        "declare @EndDate datetime = '" + DateTo + "' " +
                         unitCode +
                        "declare @balanceDate datetime = (select top(1) d.CreateDate from BalanceStock_Temp d join Stock_Temp s on d.StockId = s.StockId where @unitCode is null or s.UnitCode = @unitCode  order by d.CreateDate desc) " +
                        "select data.unitCode,data.ItemCode,ItemName, UnitQtyName, Convert(float, SUM(BeginQty)) as BeginQty, Convert(float, SUM(ReceiptQty)) ReceiptQty, Convert(float, SUM(ExpenditureQty)) ExpenditureQty, Convert(float, SUM(AdjustmentQty)) AdjustmentQty, Convert(float, SUM(OpnameQty)) as OpnameQty into #balance from( " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where @unitCode is null or UnitCode = @unitCode and TYPE = 'Balance' and[ClassificationCode] = 'BP'and Date = @balanceDate " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where @unitCode is null or UnitCode = @unitCode and TYPE = 'receipt' and[ClassificationCode] = 'BP' and(DATE > @balanceDate and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        "union all " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where @unitCode is null or UnitCode = @unitCode and TYPE = 'expenditure' and[ClassificationCode] = 'BP' and(DATE > @balanceDate and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        ")as data " +
                        "group by ItemCode, ItemName,UnitQtyName,unitCode " +
                        "select data.unitCode,data.ItemCode,ItemName, UnitQtyName,round(SUM(BeginQty), 2) as BeginQty,SUM(ReceiptQty) ReceiptQty, SUM(ExpenditureQty)ExpenditureQty,SUM(AdjustmentQty) AdjustmentQty, SUM(OpnameQty) as OpnameQty from( " +
                        "select * from #balance " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where @unitCode is null or UnitCode = @unitCode and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where @unitCode is null or UnitCode = @unitCode and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode) as data " +
                        "group by itemcode,itemname, unitqtyname,unitCode " +
                        "order by itemcode " +
                        "drop table #balance", conn))
                    {
                        SqlDataAdapter dataAdapter = new SqlDataAdapter(cmd);
                        DataSet dSet = new DataSet();
                        dataAdapter.Fill(dSet);
                        foreach (DataRow data in dSet.Tables[0].Rows)
                        {
                            FactMutationItemViewModel view = new FactMutationItemViewModel
                            {
                                unitCode = data["unitCode"].ToString(),
                                ItemCode = data["ItemCode"].ToString(),
                                ItemName = data["ItemName"].ToString(),
                                UnitQtyName = data["UnitQtyName"].ToString(),
                                BeginQty = String.Format("{0:n}", (double)data["BeginQty"]),
                                ReceiptQty = String.Format("{0:n}", (double)data["ReceiptQty"]),
                                ExpenditureQty = String.Format("{0:n}", (double)data["ExpenditureQty"]),
                                AdjustmentQty = String.Format("{0:n}", (double)data["AdjustmentQty"]),
                                OpnameQty = String.Format("{0:n}", (double)data["OpnameQty"]),
                                LastQty = String.Format("{0:n}", ((double)data["BeginQty"] + (double)data["ReceiptQty"] - (double)data["ExpenditureQty"] + (double)data["AdjustmentQty"] + (double)data["OpnameQty"])),
                                Diff = String.Format("{0:n}", 0)
                            };
                            reportData.Add(view);
                        }
                    }
                    conn.Close();
                }
            }
            catch (SqlException ex)
            {
                //Log exception
                //Display Error message
            }


            return reportData.AsQueryable();
        }

        public virtual Tuple<List<FactMutationItemViewModel>, int> GetReportBPUnit(int unit, DateTime? dateFrom, DateTime? dateTo, int page, int size, string Order, int offset)
        {
            var Query = GetUnitItemBPReport(unit, dateFrom, dateTo, offset);

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            if (OrderDictionary.Count.Equals(0))
            {
                Query = Query.OrderBy(b => b.ItemCode);
            }
            else
            {
                string Key = OrderDictionary.Keys.First();
                string OrderType = OrderDictionary[Key];

                //Query = Query.OrderBy(string.Concat(Key, " ", OrderType));
            }


            Pageable<FactMutationItemViewModel> pageable = new Pageable<FactMutationItemViewModel>(Query, page - 1, size);
            List<FactMutationItemViewModel> Data = pageable.Data.ToList<FactMutationItemViewModel>();

            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData);
        }

        public virtual MemoryStream GenerateExcelBPUnit(int unit, DateTime? dateFrom, DateTime? dateTo, int offset)
        {
            var Query = GetUnitItemBPReport(unit, dateFrom, dateTo, offset);
            Query = Query.OrderBy(b => b.ItemCode);
            DataTable result = new DataTable();
            result.Columns.Add(new DataColumn() { ColumnName = "Kode Barang", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Nama Barang", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Satuan", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Saldo Awal", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Pemasukan", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Pengeluaran", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Penyesuaian", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Saldo Akhir", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Stock Opname", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Selisih", DataType = typeof(Double) });
            if (Query.ToArray().Count() == 0)
                result.Rows.Add("", "", "", "", "", "", "", "", "", ""); // to allow column name to be generated properly for empty data as template
            else
                foreach (var item in Query)
                {
                    result.Rows.Add((item.ItemCode), item.ItemName, item.UnitQtyName, item.BeginQty, item.ReceiptQty, item.ExpenditureQty, item.AdjustmentQty, item.LastQty, item.OpnameQty, item.Diff);
                }
            return Excel.CreateExcel(new List<KeyValuePair<DataTable, string>>() { new KeyValuePair<DataTable, string>(result, "Territory") }, true);
        }

        public IQueryable<FactMutationItemViewModel> GetCentralItemBPReport( DateTime? dateFrom, DateTime? dateTo, int offset)
        {
            DateTime d1 = dateFrom == null ? new DateTime(1970, 1, 1) : (DateTime)dateFrom;
            DateTime d2 = dateTo == null ? DateTime.Now : (DateTime)dateTo;

            string DateFrom = d1.ToString("yyyy-MM-dd");
            string DateTo = d2.ToString("yyyy-MM-dd");
            List<FactMutationItemViewModel> reportData = new List<FactMutationItemViewModel>();
            try
            {
                string connectionString = APIEndpoint.ConnectionString;
                using (SqlConnection conn =
                    new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(
                        "declare @StartDate datetime = '"+ DateFrom + "' "+
                        "declare @EndDate datetime = '"+ DateTo + "' " +
						"declare @balanceDate1 datetime=(select top(1) d.Date from FactItemMutation d join ITEM_CATEGORY i on i.ItemCode=d.ItemCode join DimCategory c on c.CategoryId=i.CategoryId where ClassificationId='BB' and date <@StartDate and d.UnitCode=1  order by d.Date desc) " +
						"declare @balanceDate2 datetime=(select top(1) d.Date from FactItemMutation d join ITEM_CATEGORY i on i.ItemCode=d.ItemCode join DimCategory c on c.CategoryId=i.CategoryId where ClassificationId='BB' and date <@StartDate and d.UnitCode=2  order by d.Date desc) " +
						"declare @balanceDate3 datetime=(select top(1) d.Date from FactItemMutation d join ITEM_CATEGORY i on i.ItemCode=d.ItemCode join DimCategory c on c.CategoryId=i.CategoryId where ClassificationId='BB' and date <@StartDate and d.UnitCode=3  order by d.Date desc) " +
						"declare @balanceDate4 datetime=(select top(1) d.Date from FactItemMutation d join ITEM_CATEGORY i on i.ItemCode=d.ItemCode join DimCategory c on c.CategoryId=i.CategoryId where ClassificationId='BB' and date <@StartDate and d.UnitCode=4  order by d.Date desc) " +
						 "declare @balanceDate5 datetime=(select top(1) d.Date from FactItemMutation d join ITEM_CATEGORY i on i.ItemCode=d.ItemCode join DimCategory c on c.CategoryId=i.CategoryId where ClassificationId='BB' and date <@StartDate and d.UnitCode=5  order by d.Date desc) " +

						"select data.unitCode,data.ItemCode,ItemName, UnitQtyName, Convert(float, SUM(BeginQty)) as BeginQty, Convert(float, SUM(ReceiptQty)) ReceiptQty, Convert(float, SUM(ExpenditureQty)) ExpenditureQty, Convert(float, SUM(AdjustmentQty)) AdjustmentQty, Convert(float, SUM(OpnameQty)) as OpnameQty into #balance from( " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 1 and TYPE = 'Balance' and[ClassificationCode] = 'BP'and Date = @balanceDate1 "+
                        //"union all "+
                        //"select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 1 and TYPE = 'receipt' and[ClassificationCode] = 'BP' and(DATE > @balanceDate1 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode "+
                        //"union all "+
                        //"select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 1 and TYPE = 'expenditure' and[ClassificationCode] = 'BP' and(DATE > @balanceDate1 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode "+
                        "union all "+
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 2 and TYPE = 'Balance' and[ClassificationCode] = 'BP'and Date = @balanceDate2 " +
                        //"union all " +
                        //"select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 2 and TYPE = 'receipt' and[ClassificationCode] = 'BP' and(DATE > @balanceDate2 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        //"union all " +
                        //"select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 2 and TYPE = 'expenditure' and[ClassificationCode] = 'BP' and(DATE > @balanceDate2 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        "union all " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 3 and TYPE = 'Balance' and[ClassificationCode] = 'BP'and Date = @balanceDate3 " +
                        //"union all " +
                        //"select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 3 and TYPE = 'receipt' and[ClassificationCode] = 'BP' and(DATE > @balanceDate3 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        //"union all " +
                        //"select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 3 and TYPE = 'expenditure' and[ClassificationCode] = 'BP' and(DATE > @balanceDate3 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        "union all " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 4 and TYPE = 'Balance' and[ClassificationCode] = 'BP'and Date = @balanceDate4 " +
                        //"union all " +
                        //"select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 4 and TYPE = 'receipt' and[ClassificationCode] = 'BP' and(DATE > @balanceDate4 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        //"union all " +
                        //"select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 4 and TYPE = 'expenditure' and[ClassificationCode] = 'BP' and(DATE > @balanceDate4 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        "union all " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 5 and TYPE = 'Balance' and[ClassificationCode] = 'BP'and Date = @balanceDate5 " +
                        //"union all " +
                        //"select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 5 and TYPE = 'receipt' and[ClassificationCode] = 'BP' and(DATE > @balanceDate5 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        //"union all " +
                        //"select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 5 and TYPE = 'expenditure' and[ClassificationCode] = 'BP' and(DATE > @balanceDate5 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode) as data " +
                        "group by ItemCode, ItemName,UnitQtyName,unitCode "+
                        "select data.ItemCode,ItemName, UnitQtyName,round(SUM(BeginQty), 2) as BeginQty,SUM(ReceiptQty) ReceiptQty, SUM(ExpenditureQty)ExpenditureQty,SUM(AdjustmentQty) AdjustmentQty, SUM(OpnameQty) as OpnameQty from( "+
                        "select * from #balance " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 1 and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 2 and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 3 and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 4 and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 5 and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 1 and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 2 and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 3 and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 4 and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 5 and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BP' group by ItemCode, ItemName, UnitQtyName, unitCode) as data " +
                        "group by itemcode,itemname, unitqtyname " +
                        "order by itemcode " +
                        "drop table #balance", conn))
                    {
                        SqlDataAdapter dataAdapter = new SqlDataAdapter(cmd);
                        DataSet dSet = new DataSet();
                        dataAdapter.Fill(dSet);
                        foreach (DataRow data in dSet.Tables[0].Rows)
                        {
                            FactMutationItemViewModel view = new FactMutationItemViewModel
                            {
                                ItemCode = data["ItemCode"].ToString(),
                                ItemName = data["ItemName"].ToString(),
                                UnitQtyName = data["UnitQtyName"].ToString(),
                                BeginQty = String.Format("{0:n}", (double)data["BeginQty"]),
                                ReceiptQty = String.Format("{0:n}", (double)data["ReceiptQty"]),
                                ExpenditureQty = String.Format("{0:n}", (double)data["ExpenditureQty"]),
                                AdjustmentQty = String.Format("{0:n}", (double)data["AdjustmentQty"]),
                                OpnameQty = String.Format("{0:n}", (double)data["OpnameQty"]),
                                LastQty = String.Format("{0:n}", ((double)data["BeginQty"] + (double)data["ReceiptQty"] - (double)data["ExpenditureQty"] + (double)data["AdjustmentQty"] + (double)data["OpnameQty"])),
                                Diff = String.Format("{0:n}", 0)
                            };
                            reportData.Add(view);
                        }
                    }
                    conn.Close();
                }
            }
            catch (SqlException ex)
            {
                //Log exception
                //Display Error message
            }


            return reportData.AsQueryable();
        }

        public virtual Tuple<List<FactMutationItemViewModel>, int> GetReportBPCentral( DateTime? dateFrom, DateTime? dateTo, int page, int size, string Order, int offset)
        {
            var Query = GetCentralItemBPReport(dateFrom, dateTo, offset);

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            if (OrderDictionary.Count.Equals(0))
            {
                Query = Query.OrderBy(b => b.ItemCode);
            }
            else
            {
                string Key = OrderDictionary.Keys.First();
                string OrderType = OrderDictionary[Key];

                //Query = Query.OrderBy(string.Concat(Key, " ", OrderType));
            }


            Pageable<FactMutationItemViewModel> pageable = new Pageable<FactMutationItemViewModel>(Query, page - 1, size);
            List<FactMutationItemViewModel> Data = pageable.Data.ToList<FactMutationItemViewModel>();

            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData);
        }

        public virtual MemoryStream GenerateExcelBPCentral(DateTime? dateFrom, DateTime? dateTo, int offset)
        {
            var Query = GetCentralItemBPReport(dateFrom, dateTo, offset);
            Query = Query.OrderBy(b => b.ItemCode);
            DataTable result = new DataTable();
            result.Columns.Add(new DataColumn() { ColumnName = "Kode Barang", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Nama Barang", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Satuan", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Saldo Awal", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Pemasukan", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Pengeluaran", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Penyesuaian", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Saldo Akhir", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Stock Opname", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Selisih", DataType = typeof(Double) });
            if (Query.ToArray().Count() == 0)
                result.Rows.Add("", "", "", "", "", "", "", "", "", ""); // to allow column name to be generated properly for empty data as template
            else
                foreach (var item in Query)
                {
                    result.Rows.Add((item.ItemCode), item.ItemName, item.UnitQtyName, item.BeginQty, item.ReceiptQty, item.ExpenditureQty, item.AdjustmentQty, item.LastQty, item.OpnameQty, item.Diff);
                }
            return Excel.CreateExcel(new List<KeyValuePair<DataTable, string>>() { new KeyValuePair<DataTable, string>(result, "Territory") }, true);
        }

        public IQueryable<FactMutationItemViewModel> GetCentralItemBBReport(DateTime? dateFrom, DateTime? dateTo, int offset)
        {
            DateTime d1 = dateFrom == null ? new DateTime(1970, 1, 1) : (DateTime)dateFrom;
            DateTime d2 = dateTo == null ? DateTime.Now : (DateTime)dateTo;

            string DateFrom = d1.ToString("yyyy-MM-dd");
            string DateTo = d2.ToString("yyyy-MM-dd");
            List<FactMutationItemViewModel> reportData = new List<FactMutationItemViewModel>();
            try
            {
                string connectionString = APIEndpoint.ConnectionString;
                using (SqlConnection conn =
                    new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(
                        "declare @StartDate datetime = '" + DateFrom + "' " +
                        "declare @EndDate datetime = '" + DateTo + "' " +
					   "declare @balanceDate1 datetime=(select top(1) d.Date from FactItemMutation d join ITEM_CATEGORY i on i.ItemCode=d.ItemCode join DimCategory c on c.CategoryId=i.CategoryId where ClassificationId='BB' and date <@StartDate and d.UnitCode=1  order by d.Date desc) " +
						"declare @balanceDate2 datetime=(select top(1) d.Date from FactItemMutation d join ITEM_CATEGORY i on i.ItemCode=d.ItemCode join DimCategory c on c.CategoryId=i.CategoryId where ClassificationId='BB' and date <@StartDate and d.UnitCode=2  order by d.Date desc) " +
						"declare @balanceDate3 datetime=(select top(1) d.Date from FactItemMutation d join ITEM_CATEGORY i on i.ItemCode=d.ItemCode join DimCategory c on c.CategoryId=i.CategoryId where ClassificationId='BB' and date <@StartDate and d.UnitCode=3  order by d.Date desc) " +
						"declare @balanceDate4 datetime=(select top(1) d.Date from FactItemMutation d join ITEM_CATEGORY i on i.ItemCode=d.ItemCode join DimCategory c on c.CategoryId=i.CategoryId where ClassificationId='BB' and date <@StartDate and d.UnitCode=4  order by d.Date desc) " +
						"declare @balanceDate5 datetime=(select top(1) d.Date from FactItemMutation d join ITEM_CATEGORY i on i.ItemCode=d.ItemCode join DimCategory c on c.CategoryId=i.CategoryId where ClassificationId='BB' and date <@StartDate and d.UnitCode=5  order by d.Date desc)  " +
						"select data.unitCode,data.ItemCode,ItemName, UnitQtyName, Convert(float, SUM(BeginQty)) as BeginQty, Convert(float, SUM(ReceiptQty)) ReceiptQty, Convert(float, SUM(ExpenditureQty)) ExpenditureQty, Convert(float, SUM(AdjustmentQty)) AdjustmentQty, Convert(float, SUM(OpnameQty)) as OpnameQty into #balance from( " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 1 and TYPE = 'Balance' and[ClassificationCode] = 'BB'and Date = @balanceDate1 " +
                        //"union all " +
                        //"select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 1 and TYPE = 'receipt' and[ClassificationCode] = 'BB' and(DATE > @balanceDate1 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        //"union all " +
                        //"select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 1 and TYPE = 'expenditure' and[ClassificationCode] = 'BB' and(DATE > @balanceDate1 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        "union all " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 2 and TYPE = 'Balance' and[ClassificationCode] = 'BB'and Date = @balanceDate2 " +
                        //"union all " +
                        //"select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 2 and TYPE = 'receipt' and[ClassificationCode] = 'BB' and(DATE > @balanceDate2 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        //"union all " +
                        //"select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 2 and TYPE = 'expenditure' and[ClassificationCode] = 'BB' and(DATE > @balanceDate2 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        "union all " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 3 and TYPE = 'Balance' and[ClassificationCode] = 'BB'and Date = @balanceDate3 " +
                        //"union all " +
                        //"select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 3 and TYPE = 'receipt' and[ClassificationCode] = 'BB' and(DATE > @balanceDate3 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        //"union all " +
                        //"select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 3 and TYPE = 'expenditure' and[ClassificationCode] = 'BB' and(DATE > @balanceDate3 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        "union all " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 4 and TYPE = 'Balance' and[ClassificationCode] = 'BB'and Date = @balanceDate4 " +
                        //"union all " +
                        //"select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 4 and TYPE = 'receipt' and[ClassificationCode] = 'BB' and(DATE > @balanceDate4 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        //"union all " +
                        //"select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 4 and TYPE = 'expenditure' and[ClassificationCode] = 'BB' and(DATE > @balanceDate4 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        "union all " +
                        "select unitCode,ItemCode, ItemName,UnitQtyName, (Quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 5 and TYPE = 'Balance' and[ClassificationCode] = 'BB'and Date = @balanceDate5 " +
                        //"union all " +
                        //"select unitCode, ItemCode, ItemName, UnitQtyName, SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 5 and TYPE = 'receipt' and[ClassificationCode] = 'BB' and(DATE > @balanceDate5 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode " +
                        //"union all " +
                        //"select unitCode,ItemCode, ItemName,UnitQtyName,-SUM(quantity) as BeginQty,0 as ReceiptQty,0 as ExpenditureQty,0 as AdjustmentQty,0 as OpnameQty from FactItemMutation where UnitCode = 5 and TYPE = 'expenditure' and[ClassificationCode] = 'BB' and(DATE > @balanceDate5 and date < @StartDate) group by ItemCode, ItemName,UnitQtyName ,unitCode) as data " +
                        "group by ItemCode, ItemName,UnitQtyName,unitCode " +
                        "select data.ItemCode,ItemName, UnitQtyName,round(SUM(BeginQty), 2) as BeginQty,SUM(ReceiptQty) ReceiptQty, SUM(ExpenditureQty)ExpenditureQty,SUM(AdjustmentQty) AdjustmentQty, SUM(OpnameQty) as OpnameQty from( " +
                        "select * from #balance " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 1 and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 2 and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 3 and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 4 and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, 0 as ReceiptQty, SUM(quantity) as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 5 and TYPE = 'expenditure' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 1 and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 2 and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 3 and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 4 and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode " +
                        "union all " +
                        "select unitCode, ItemCode, ItemName, UnitQtyName, 0 as BeginQty, SUM(quantity) as ReceiptQty, 0 as ExpenditureQty, 0 as AdjustmentQty, 0 as OpnameQty from FactItemMutation where UnitCode = 5 and TYPE = 'receipt' and DATE between @StartDate and @EndDate and[ClassificationCode] = 'BB' group by ItemCode, ItemName, UnitQtyName, unitCode) as data " +
                        "group by itemcode,itemname, unitqtyname " +
                        "order by itemcode " +
                        "drop table #balance", conn))
                    {
                        SqlDataAdapter dataAdapter = new SqlDataAdapter(cmd);
                        DataSet dSet = new DataSet();
                        dataAdapter.Fill(dSet);
                        foreach (DataRow data in dSet.Tables[0].Rows)
                        {
                            FactMutationItemViewModel view = new FactMutationItemViewModel
                            {
                                ItemCode = data["ItemCode"].ToString(),
                                ItemName = data["ItemName"].ToString(),
                                UnitQtyName = data["UnitQtyName"].ToString(),
                                BeginQty = String.Format("{0:n}", (double)data["BeginQty"]),
                                ReceiptQty = String.Format("{0:n}", (double)data["ReceiptQty"]),
                                ExpenditureQty = String.Format("{0:n}", (double)data["ExpenditureQty"]),
                                AdjustmentQty = String.Format("{0:n}", (double)data["AdjustmentQty"]),
                                OpnameQty = String.Format("{0:n}", (double)data["OpnameQty"]),
                                LastQty = String.Format("{0:n}", ((double)data["BeginQty"] + (double)data["ReceiptQty"] - (double)data["ExpenditureQty"] + (double)data["AdjustmentQty"] + (double)data["OpnameQty"])),
                                Diff = String.Format("{0:n}", 0)
                            };
                            reportData.Add(view);
                        }
                    }
                    conn.Close();
                }
            }
            catch (SqlException ex)
            {
                //Log exception
                //Display Error message
            }


            return reportData.AsQueryable();
        }

        public virtual Tuple<List<FactMutationItemViewModel>, int> GetReportBBCentral(DateTime? dateFrom, DateTime? dateTo, int page, int size, string Order, int offset)
        {
            var Query = GetCentralItemBBReport(dateFrom, dateTo, offset);

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            if (OrderDictionary.Count.Equals(0))
            {
                Query = Query.OrderBy(b => b.ItemCode);
            }
            else
            {
                string Key = OrderDictionary.Keys.First();
                string OrderType = OrderDictionary[Key];

                //Query = Query.OrderBy(string.Concat(Key, " ", OrderType));
            }


            Pageable<FactMutationItemViewModel> pageable = new Pageable<FactMutationItemViewModel>(Query, page - 1, size);
            List<FactMutationItemViewModel> Data = pageable.Data.ToList<FactMutationItemViewModel>();

            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData);
        }

        public virtual MemoryStream GenerateExcelBBCentral(DateTime? dateFrom, DateTime? dateTo, int offset)
        {
            var Query = GetCentralItemBBReport(dateFrom, dateTo, offset);
            Query = Query.OrderBy(b => b.ItemCode);
            DataTable result = new DataTable();
            result.Columns.Add(new DataColumn() { ColumnName = "Kode Barang", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Nama Barang", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Satuan", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Saldo Awal", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Pemasukan", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Pengeluaran", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Penyesuaian", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Saldo Akhir", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Stock Opname", DataType = typeof(Double) });
            result.Columns.Add(new DataColumn() { ColumnName = "Selisih", DataType = typeof(Double) });
            if (Query.ToArray().Count() == 0)
                result.Rows.Add("", "", "", "", "", "", "", "", "", ""); // to allow column name to be generated properly for empty data as template
            else
                foreach (var item in Query)
                {
                    result.Rows.Add((item.ItemCode), item.ItemName, item.UnitQtyName, item.BeginQty, item.ReceiptQty, item.ExpenditureQty, item.AdjustmentQty, item.LastQty, item.OpnameQty, item.Diff);
                }
            return Excel.CreateExcel(new List<KeyValuePair<DataTable, string>>() { new KeyValuePair<DataTable, string>(result, "Territory") }, true);
        }
    }
}
