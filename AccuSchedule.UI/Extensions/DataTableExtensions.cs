using AccuSchedule.UI.Methods;
using ClosedXML.Report;
using FastMember;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccuSchedule.UI.Extensions
{
    public static class DataTableExtensions
    {
        public static string CreateTableSearchQuery(this DataTable table, string searchText, bool caseInvariant = true)
        {
            StringBuilder sb = new StringBuilder();


                foreach (DataColumn column in table.Columns)
                {
                    bool addBrackets = false;
                    var charsThatRequireBrackets = @"~()#\/=><+-*%&|^'""[ ]";
                    for (int i = 0; i < column.ColumnName.Length; i++)
                        if (charsThatRequireBrackets.Contains(column.ColumnName.ElementAt(i)))
                        {
                            addBrackets = true;
                            break;
                        }
                    var colName = addBrackets ? "[" + column.ColumnName + "]" : column.ColumnName;
                    if (colName.ToLower() == "parent" && !colName.Contains("[")) 
                        colName = "[" + column.ColumnName + "]"; // Bracket protected names

                if (!string.IsNullOrEmpty(searchText))
                {
                    if (caseInvariant)
                        sb.AppendFormat("CONVERT({0}, System.String) Like '%{1}%' OR ", colName, EscapeSqlLikeValue(searchText));
                    else
                        sb.AppendFormat("CONVERT({0}, System.String) = '%{1}%' OR ", colName, EscapeSqlLikeValue(searchText));
                }
                else sb.AppendFormat("Isnull({0},'') <> '' OR", colName);

            }
            sb.Remove(sb.Length - 3, 3); // Removes the last OR statement

            return sb.ToString();
        }
        public static string CreateTableSearchQuery(this string[] stringArr, string searchText, bool matchExact = false)
        {
            StringBuilder sb = new StringBuilder();

            if (!string.IsNullOrEmpty(searchText))
            {
                foreach (var name in stringArr)
                {
                    bool addBrackets = false;
                    var charsThatRequireBrackets = @"~()#\/=><+-*%&|^'""[ ]";
                    for (int i = 0; i < name.Length; i++)
                        if (charsThatRequireBrackets.Contains(name.ElementAt(i)))
                        {
                            addBrackets = true;
                            break;
                        }


                    var colName = addBrackets ? "[" + name + "]" : name;
                    if (matchExact)
                        sb.AppendFormat("CONVERT({0}, System.String) Like '%{1}%' OR ", colName, EscapeSqlLikeValue(searchText));
                    else
                        sb.AppendFormat("CONVERT({0}, System.String) = '%{1}%' OR ", colName, EscapeSqlLikeValue(searchText));
                }


                sb.Remove(sb.Length - 3, 3); // Removes the last OR statement
            }

            return sb.ToString();
        }
        public static IEnumerable<DataRow> RemoveRowsFromTableContainingValue(this DataTable Table, string[] FilterValues, string[] Columns = null, bool caseInsensitive = true, bool matchExact = false)
        {
            var toRemove = Table.FindRowsInDataTable(FilterValues, Columns, caseInsensitive, matchExact);
            if (!toRemove.Any()) return null;

            var toRemoveRet = toRemove.CopyToDataTable().AsEnumerable();

            // Remove the rows
            foreach (DataRow row in toRemove)
                Table.Rows.Remove(row);

            return toRemoveRet;
        }
        public static IEnumerable<DataRow> RemoveRowsFromTableContainingValue(this IEnumerable<DataRow> FilteredRows, string ColumnToBuildSearchListFrom, string[] ColsToSearch, DataTable FromTable, bool caseInsensitive = true, bool matchExact = false)
        {
            if (FilteredRows == null || !FilteredRows.Any()) return new List<DataRow>();

            var ToFind = FilteredRows.Select(w => w.Field<object>(ColumnToBuildSearchListFrom).ToString()).ToArray();
            var FilteredByResults = FromTable.FindRowsInDataTable(ToFind, ColsToSearch, caseInsensitive, matchExact);
            var FilteredReturnBeforeDelete = FilteredByResults.Any() ? FilteredByResults.CopyToDataTable().AsEnumerable() : null;

            // Remove each of the rows
            FilteredByResults.ForEach(row => FromTable.Rows.Remove(row));

            return FilteredReturnBeforeDelete;
        }
        public static string[] ColumnNames(this DataTable table, string onlyContaining = "", bool caseInsensitive = true, bool exactMatch = false)
        {
            var cols = new HashSet<string>();

            foreach (DataColumn col in table.Columns)
            {
                string ret = col.ColumnName.ToString();

                if (caseInsensitive)
                {
                    if (exactMatch)
                    {
                        if (ret.ToLower() == onlyContaining)
                            cols.Add(col.ColumnName);
                    }
                    else if (ret.ToLower().Contains(onlyContaining))
                        cols.Add(col.ColumnName);
                }
                else
                {
                    if (exactMatch)
                    {
                        if (ret == onlyContaining)
                            cols.Add(col.ColumnName);
                    }
                    else if (ret.Contains(onlyContaining))
                        cols.Add(col.ColumnName);
                }
            }
                



            return cols.ToArray();
        }
        public static bool DoesValueExistInColumns(this DataRow dataRow, string[] colsToSearch, string Find)
        {
            if (string.IsNullOrEmpty(Find)) return false;

            foreach (var col in colsToSearch)
                if (dataRow.Field<object>(col)?.ToString() == Find.ToLower())
                    return true;

            return false;
        }
        public static string AnyEmptyValueInColumns(this DataRow dataRow, string[] colsToSearch)
        {
            foreach (var col in colsToSearch)
                if (dataRow.Table.Columns.Contains(col) && string.IsNullOrEmpty(dataRow.Field<object>(col)?.ToString()))
                    return col;

            return null;
        }

        public static DataTable NewTableWithOrganizedColumns(params Dictionary<string, object>[] dicts)
        {
            if (dicts == null) return new DataTable();

            var newTable = new DataTable();

            var combinedDict = new Dictionary<string, object>();
            foreach (var dict in dicts)
                dict.ForEach(f => combinedDict.Add(f.Key, f.Value));


            if (combinedDict != null)
            {
                foreach (var colName in combinedDict)
                {
                    var newCol = new DataColumn() { ColumnName = colName.Key };
                    newTable.Columns.Add(newCol);
                }
                // Set the positioning of the columns
                foreach (string col in newTable.ColumnNames())
                    if (combinedDict.ContainsKey(col))
                        newTable.Columns[col].SetOrdinal(Convert.ToInt32(combinedDict[col]));
            }

            return newTable;
        }

        public static string EscapeSqlLikeValue(string value, bool ignoreSingleQuote = false)
        {
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case ']':
                    case '[':
                    case '%':
                    case '*':
                        sb.Append("[").Append(c).Append("]");
                        break;
                    case '\'':
                        if (!ignoreSingleQuote)
                            sb.Append("''");
                        else
                            sb.Append("'");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
        public static IEnumerable<DataRow> FindRowsInDataTable(this DataTable Table, string[] FilterValue, string[] ColumnName = null, bool caseInvariant = true, bool matchExact = false)
        {
            List<DataRow> foundRows = new List<DataRow>();

            foreach (var filterString in FilterValue)
            {
                var Search = caseInvariant ? filterString.ToLower() : filterString;
                foreach (var colName in ColumnName)
                {
                    if (!string.IsNullOrEmpty(colName))
                    { // Search just the column name
                        if (!string.IsNullOrEmpty(Search) || Search == " ")
                        {
                            var filterRows = Table.AsEnumerable()
                                .Where(w =>
                                matchExact
                                ? caseInvariant
                                    ? w.Field<object>(colName).ToString().ToLower() == Search
                                    : w.Field<object>(colName).ToString() == Search
                                : caseInvariant
                                    ? w.Field<object>(colName).ToString().ToLower().Contains(Search)
                                    : w.Field<object>(colName).ToString().Contains(Search));

                            filterRows.ForEach(f => foundRows.Add(f));
                        }
                        else
                        {
                            var filterRows = Table.AsEnumerable()
                                .Where(w => string.IsNullOrEmpty(w.Field<object>(colName)?.ToString()?.Trim()));

                            filterRows.ForEach(f => foundRows.Add(f));
                        }
                        
                    }
                    else
                    { // Search all the columns
                        var rowFilter = DataTableExtensions.CreateTableSearchQuery(Table, filterString);
                        var filterRows = Table.Select(rowFilter).AsEnumerable();
                        filterRows.ForEach(f => foundRows.Add(f));
                    }
                }

            }

            return foundRows.Distinct();
        }

        public static DataTable CopyGroupingToTableWithNewIDColumn(DataTable orderTable, IEnumerable<IGrouping<dynamic, DataRow>> query, string TableName, string groupIdentifier, string ColNameToMatch)
        {
            var retTable = new DataTable();

            int groupID = -1;
            var groupedTable = orderTable.Copy();
            var newCol = !groupedTable.Columns.Contains(groupIdentifier) ? groupedTable.Columns.Add(groupIdentifier) : null;
            if (newCol != null) newCol.SetOrdinal(0);

            foreach (IGrouping<dynamic, DataRow> grouping in query)
            {
                groupID += 1;

                // Get the values of the grouping
                var items = grouping.Select(s => {

                    var retDict = new Dictionary<string, object>();

                    // Get each column and add it to the new object
                    foreach (DataColumn col in s.Table.Columns)
                        retDict[col.ColumnName] = s.Field<object>(col.ColumnName)?.ToString().Trim();


                    return retDict;
                });

                var dictList = items.SelectMany(dict => dict).GroupBy(kvp => kvp.Key).ToDictionary(g => g.Key, v => v);

                IGrouping<string, KeyValuePair<string, object>> ordersResult = null;
                var orders = dictList.TryGetValue(ColNameToMatch, out ordersResult);

                // Find the value from the original orders
                if (orders)
                {
                    foreach (var item in ordersResult)
                    {
                        var ordersList = groupedTable.AsEnumerable().Where(w => w.Field<object>(ColNameToMatch).ToString().Trim() == item.Value.ToString().Trim());

                        if (ordersList != null && ordersList.Any())
                            foreach (DataColumn gCol in groupedTable.Columns)
                                if (gCol.ColumnName == groupIdentifier)
                                    foreach (var order in ordersList)
                                        order.SetField(gCol, groupID); // Check if groupID has already been added



                    }
                }
            }

            retTable.TableName = TableName;
            retTable = groupedTable;
            return retTable;
        }
        public static DataTable CopyNewGroupIDsToAnotherTable(DataTable origTable, DataTable newGroupedTable, string groupIdentifier, string[] groupByCols)
        {
            var newOrdersTable = origTable.Copy();
            var newCol = new DataColumn() { ColumnName = groupIdentifier };
            newOrdersTable.Columns.Add(newCol);
            newCol.SetOrdinal(0);

            // Get a distinct list of Orders Numbers and join the 
            var AllOrderNumsAndIDs = new List<dynamic>();
            if (groupByCols.Count() == 1)
            {
                var orderNumsAndIDs = newGroupedTable.AsEnumerable()
                    .GroupBy(g => new DynamicDataRowGroup(g, groupByCols))
                    .Select(s => new
                    {
                        Key = s?.Key,
                        GroupIDs = string.Join("|", s?.Select(w => w.Field<object>(groupIdentifier)?.ToString().Trim())?.Distinct())
                    }).ToList();

                AllOrderNumsAndIDs.AddRange(orderNumsAndIDs);
            }
            else
            {
                var orderNumsAndIDs = newGroupedTable.AsEnumerable()
                    .GroupBy(g => new DynamicDataRowGroup(g, groupByCols))
                    .Select(s => new
                    {
                        Key = s.Key,
                        GroupIDs = string.Join("|", s.Select(w => w.Field<object>(groupIdentifier)?.ToString().Trim()).Distinct())
                    }).ToList();

                AllOrderNumsAndIDs.AddRange(orderNumsAndIDs);
            }




            // Make sure all values from each dictionaries have a match
            foreach (var orderRow in newOrdersTable.AsEnumerable())
            {
                var againstValues = new Dictionary<string, object>();
                var matches = AllOrderNumsAndIDs.AsEnumerable().Where(w => ConvertToDictAndCompare(w.Key, orderRow, groupByCols));
                foreach (var match in matches)
                {
                    foreach (var groupingCol in groupByCols)
                    {
                        var matchkey = match.Key as DynamicDataRowGroup;
                        if (matchkey != null)
                        {
                            var matchDict = matchkey.ToDictionary();
                            againstValues[groupingCol] = matchDict[groupingCol].ToString().Trim();
                        }
                    }

                }

                if (againstValues.Any())
                {
                    var groupedMat = AllOrderNumsAndIDs
                        .Where(w =>
                            DelimetedStringContains(w.Key, againstValues))
                        .Select(s => s.GroupIDs);

                    var groupIDs = groupedMat.Any() ? string.Join("|", groupedMat.Cast<string>().Distinct()) : string.Empty;

                    orderRow.SetField(newOrdersTable.Columns[groupIdentifier], groupIDs);
                }

            }
            return newOrdersTable;
        }
        public static bool ConvertToDictAndCompare(DynamicDataRowGroup rowGroup, DataRow rowOrder, string[] groupItems)
        {
            var groupDict = rowGroup.ToDictionary();
            var allValues = true;
            foreach (var groupItem in groupItems)
            {
                if ((string)groupDict[groupItem] != rowOrder.Field<object>(groupItem).ToString())
                    return false;
            }
            return allValues;

        }
        /// <param name="DelimToCheck">Dynamic Grouping</param>
        /// <param name="Against">Key is Column Name, Value is Rows Value</param>
        /// <param name="Delim">Deliminator</param>
        /// <returns></returns>
        public static bool DelimetedStringContains(DynamicDataRowGroup DelimToCheck, Dictionary<string, object> AgainstDict, char Delim = '|')
        {
            var checkRow = DelimToCheck.ToDictionary();

            // Populate the values from each Column in the row
            var toCheckVals = new Dictionary<string, object>();
            var AgainstVals = new Dictionary<string, object>();
            foreach (var ToCheckColName in checkRow.Keys)
            {
                var toCheckVal = checkRow.Where(w =>
                    w.Key == ToCheckColName);
                if (toCheckVal.Any())
                    foreach (var item in toCheckVal)
                    {
                        if (!string.IsNullOrEmpty(item.Key))
                            toCheckVals[item.Key] = item.Value;
                    }


                var AgainstVal = AgainstDict.Where(w =>
                    w.Key == ToCheckColName);

                if (AgainstVal.Any())
                    foreach (var item in AgainstVal)
                    {
                        if (!string.IsNullOrEmpty(item.Key))
                            AgainstVals[item.Key] = item.Value;
                    }
            }
            toCheckVals.Values.Distinct();
            AgainstVals.Values.Distinct();

            var anyMatches = false;

            // Make sure the keys match
            if (toCheckVals.Any() && AgainstVals.Any()
                && toCheckVals.Keys.Count() == AgainstVals.Keys.Count())
            {
                // Find the matching Against Key and compare values
                foreach (var check in toCheckVals)
                {
                    var againstList = AgainstVals.Where(w => w.Key == check.Key);
                    foreach (var againstItem in againstList)
                    {
                        anyMatches = againstItem.Value != null ? DelimetedStringContains(check.Value.ToString(), againstItem.Value.ToString()) : false;

                        if (!anyMatches) break;
                    }

                }

            }

            return anyMatches;
        }
        public static bool DelimetedStringContains(string DelimToCheck, string Against, char Delim = '|')
        {

            // Format toCheck into array
            HashSet<string> delimsToCheck = new HashSet<string>();
            if (DelimToCheck.Contains(Delim))
                delimsToCheck = DelimToCheck.Split(Delim).Distinct().ToHashSet();
            else
                if (!string.IsNullOrEmpty(DelimToCheck))
                delimsToCheck.Add(DelimToCheck);

            // Format Against into array
            HashSet<string> delimsAgainst = new HashSet<string>();
            if (Against.Contains(Delim))
                delimsAgainst = Against.Split(Delim).Distinct().ToHashSet();
            else
                if (!string.IsNullOrEmpty(Against))
                delimsAgainst.Add(Against);


            // Check if each value from DelimToCheck is inside Against
            bool ret = true;
            foreach (var item in delimsToCheck)
                if (!delimsAgainst.Contains(item)) ret = false;

            return ret;
        }



        public static void AddOrUpdateTable(this DataSet _set, DataTable table)
        {
            if (table != null)
            {
                if (!_set.Tables.Contains(table.TableName))
                {
                    _set.Tables.Add(table);
                }
                else
                {
                    _set.Tables.Remove(_set.Tables[table.TableName]);
                    _set.Tables.Add(table);
                }
            }
        }

        public static DataTable SerializeToDataTable (this List<dynamic> dynObjects) 
        {
            var json = JsonConvert.SerializeObject(dynObjects);
            return (DataTable)JsonConvert.DeserializeObject(json, (typeof(DataTable)));
        }
        public static List<dynamic> SerializeToDynamicEnumerable(this DataTable dtObj)
        {
            var json = JsonConvert.SerializeObject(dtObj);
            return (List<dynamic>)JsonConvert.DeserializeObject(json, (typeof(DataTable)));
        }


        public static Dictionary<string, object> GetDict(this DataTable dt)
        {
            return dt.AsEnumerable()
              .ToDictionary<DataRow, string, object>(row => row.Field<string>(0),
                                        row => row.Field<object>(1));
        }

        public static DataTable ToTable(this IList source)
        {
            if (source == null) return null;

            var table = new DataTable();
            if (source.Count == 0) return table;

            // blatently assume the list is homogeneous
            Type itemType = source[0].GetType();
            table.TableName = itemType.Name;
            List<string> names = new List<string>();
            foreach (var prop in itemType.GetProperties())
            {
                if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                {
                    names.Add(prop.Name);
                    table.Columns.Add(prop.Name, prop.PropertyType);
                }
            }
            names.TrimExcess();

            var accessor = TypeAccessor.Create(itemType);
            object[] values = new object[names.Count];
            foreach (var row in source)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = accessor[row, names[i]];
                }
                table.Rows.Add(values);
            }
            return table;
        }


        public static TemplateErrors ToTemplateEngine(this DataView data, params KeyValuePair<string, string>[] addParams)
        {
            if (data == null || data.Table.Rows.Count == 0) return null;

            // Get Template File
            var template = FileExtensions.OpenFileOrNull("Open Template File", "xlsx", "Excel File (*.xlsx)|*.xlsx");
            if (template == null) return null;

            // Fire the engine
            var engine = new XLTemplate(template.FileName);
            engine.AddVariable(data.Table.TableName, data.Table.Rows.Cast<DataRow>());

            if (addParams != null && addParams.Length > 0)
                foreach (var param in addParams)
                    engine.AddVariable(param.Key, param.Value);

            var results = engine.Generate();
            var errors = results.ParsingErrors;


            // Save the file
            var saveFile = FileExtensions.SaveAsOrNull("Save As...", "xlsx", "Excel File (*.xlsx)|*.xlsx");
            if (saveFile != null)
            {
                engine.SaveAs(saveFile.FileName);
                // Show the file
                Process.Start(new ProcessStartInfo(saveFile.FileName) { UseShellExecute = true });
            }

            return errors;
        }


        
    }
}
