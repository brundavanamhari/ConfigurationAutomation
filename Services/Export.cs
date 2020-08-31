using SumTotal.Framework.Container;
using SumTotal.Framework.Data;
using SumTotal.Models.Configuration.Settings;
using SumTotal.Repository.Contracts.Infra;
using SumTotal.Services.DataContracts.Core.Lookups;
using SumTotal.Services.Facade.Contracts.Core.Lookups;
using SumTotal.Services.Jobs.Contracts;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace Sumtotal.ConfigurationsAutomation.Services
{
    public class Export : BaseExtract
    {
        ServiceJobContext jobContext = new ServiceJobContext();
        public Export()
        {

        }

        public override void Execute(ServiceJobContext context, IDictionary<string, object> parameters)
        {
            string currentTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            _logger.LogInfo($"Execution Proces Started for {context.JobKey} at {currentTime}.");
            dataProvider = SumtContainer.Resolve<IDataProvider>();
            dataProvider.Open();

            try
            {
                int masterCategoryCode;
                string group;
                string settingstoExport = string.Empty;
                //Load job parameters
                masterCategoryCode = Convert.ToInt32(parameters["MasterCategoryCode"]);
                group = parameters["Group"].ToString();
                settingstoExport = parameters["SettingsExport"].ToString();
                string reportPath = parameters["ExtractPath"].ToString();
                string orgCodestoExport = parameters["OrgCodestoExport"].ToString();
                DataTable dtOrgDetails = GetOrgDetails(orgCodestoExport);
                if (string.IsNullOrEmpty(settingstoExport))
                {
                ISystemJobRepository systemJobRepository = SumtContainer.Resolve<ISystemJobRepository>();
                ICodeDefinitionFacade facade = SumtContainer.Resolve<ICodeDefinitionFacade>();
                CodeDefinitionDTO codeDefinition = facade.GetCodeDefinitionWithAttributes(masterCategoryCode);
                    List<string> settings = settingstoExport.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    IList<CodeDTO> codes = codeDefinition.Codes.Where(c => settings.Any(b => string.Compare(c.ItemCode, b, true) == 0)).ToList();
                    string domainCondition = "";
                    if (group == "I")
                    {
                string sectionLookupClause = string.Empty;
                foreach (CodeDTO code in codes)
                {
                    String[] sectionLookups = code.CodeAttributeDTO.Attr2Val.Split(',');
                    for (int i = 0; i < sectionLookups.Length; i++)
                    {
                                if (dtOrgDetails != null && dtOrgDetails.Rows.Count != 0) 
                                {
                                    domainCondition = "";
                                    foreach(DataRow row in dtOrgDetails.Rows)
                                    {
                                        domainCondition += " p.Section like '%" + "Domain/" + row[0] + "/" + sectionLookups[i]  + "%'";
                                    }
                                    sectionLookups[i] = domainCondition;
                                }
                                else
                                {
                        sectionLookups[i] = " p.Section like '%" + sectionLookups[i] + "%'";
                    }
                            }
                    sectionLookupClause += String.Join(" OR ", sectionLookups);
                }
                GetPersistData(reportPath, sectionLookupClause);
                    }
                    else if(group == "II")
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while processing configurations extract.", ex);
                throw (ex);
            }
            finally
            {
                dataProvider.Close();
            }

        }
        private DataTable GetOrgDetails(string OrgCodesToExport)
        {
            DataTable dt = new DataTable();
            string cmdOrgDetails = "Select OrganizationPK,code from Organization";
            cmdOrgDetails = string.IsNullOrEmpty(OrgCodesToExport) ? cmdOrgDetails : cmdOrgDetails + " Where Code in ('" + OrgCodesToExport.Replace(",", "','") + "')";
            DataSet dataSet = dataProvider.ExecuteSelectSql(cmdOrgDetails);
            dt = dataSet.Tables[0];
            dt.PrimaryKey = new DataColumn[] {
                    dt.Columns["code"]
                };
            return dt;
        }
        private void GetPersistData(string reportPath, string sectionClause)
        {
            try
            {
                string sqlCommand = @"SELECT rtrim(ltrim(cast(App as varchar(50)))) +','+ Scope +','+Section +','+replace(Data,char(13)+char(10),'<<<<>>>') as settings from Persist p
                             where " + sectionClause;

                DataSet dataSet = dataProvider.ExecuteSelectSql(sqlCommand);
                DataTable dt = dataSet.Tables[0];
                var finalPath = Path.Combine(reportPath, "Persist_ApprovalConfig" + DateTime.Now.ToString("yyyyMMdd'_'HHmmss") + ".txt");
                IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().
                                  Select(column => column.ColumnName);
                StringBuilder sb = new StringBuilder();
                //sb.AppendLine(string.Join(",", columnNames));
                foreach (DataRow row in dt.Rows)
                {
                    string[] fields = row.ItemArray.Select(field => field.ToString()).
                                                    ToArray();
                    sb.AppendLine(string.Join(",", fields));
                }
                File.WriteAllText(finalPath, sb.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error Occoured in ClassName {nameof(Export)} Method {nameof(GetPersistData)}" + ex.Message);

            }
        }
    }
}
