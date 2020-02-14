using GdcoTicket.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Utilities;

namespace GdcoTicket
{
    public class GdcoTicketTableHandler
    {
        public List<GdcoTableTicket> GetTickets(string projectId, string source)
        {
            List<GdcoTableTicket> gdcoTableTicketList = new List<GdcoTableTicket>();
            if (string.IsNullOrEmpty(projectId))
            {
                return gdcoTableTicketList;
            }
            using (SqlConnection connection = new SqlConnection(this.GetConnectionString()))
            {
                try
                {
                    SqlCommand sqlCommand = new SqlCommand(this.GetQuery(projectId, source), connection);
                    sqlCommand.CommandType = CommandType.Text;
                    connection.Open();
                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(sqlCommand);
                    DataTable dataTable = new DataTable();
                    sqlDataAdapter.Fill(dataTable);
                    if (dataTable.Rows.Count > 0)
                    {
                        for (int index = 0; index < dataTable.Rows.Count; ++index)
                        {
                            DataRow row = dataTable.Rows[index];
                            gdcoTableTicketList.Add(new GdcoTableTicket()
                            {
                                Id = Convert.ToInt64(row["Id"]),
                                ProjectId = row.IsNull("ProjectId") ? (string)null : row["ProjectId"].ToString().Trim(),
                                GdcoTicket = row.IsNull("GdcoTicket") ? (string)null : row["GdcoTicket"].ToString().Trim()
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Exception in GetTickets", ex);
                }
                finally
                {
                    connection.Close();
                }
            }
            return gdcoTableTicketList;
        }

        public Dictionary<string, List<GdcoTableTicket>> GetTickets(List<string> projectIds, string source)
        {
            var gdcoTableTicketDictionary = new Dictionary<string, List<GdcoTableTicket>>();
            if (projectIds == null || !projectIds.Any())
            {
                return gdcoTableTicketDictionary;
            }
            using (SqlConnection connection = new SqlConnection(this.GetConnectionString()))
            {
                try
                {
                    SqlCommand sqlCommand = new SqlCommand(this.GetQuery(projectIds, source), connection);
                    sqlCommand.CommandType = CommandType.Text;
                    connection.Open();
                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(sqlCommand);
                    DataTable dataTable = new DataTable();
                    sqlDataAdapter.Fill(dataTable);
                    if (dataTable.Rows.Count > 0)
                    {
                        for (int index = 0; index < dataTable.Rows.Count; ++index)
                        {
                            DataRow row = dataTable.Rows[index];
                            var Id = Convert.ToInt64(row["Id"]);
                            var ProjectId = row.IsNull("ProjectId") ? null : row["ProjectId"].ToString().Trim();
                            var GdcoTicket = row.IsNull("GdcoTicket") ? null : row["GdcoTicket"].ToString().Trim();

                            if (!gdcoTableTicketDictionary.ContainsKey(ProjectId))
                            {
                                gdcoTableTicketDictionary[ProjectId] = new List<GdcoTableTicket> {
                                    new GdcoTableTicket
                                    {
                                        Id = Id,
                                        ProjectId = ProjectId,
                                        GdcoTicket = GdcoTicket
                                    }
                                };
                            }
                            else
                            {
                                var ticketList = gdcoTableTicketDictionary[ProjectId];
                                ticketList.Add(new GdcoTableTicket
                                {
                                    Id = Id,
                                    ProjectId = ProjectId,
                                    GdcoTicket = GdcoTicket
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Exception in GetTickets", ex);
                }
                finally
                {
                    connection.Close();
                }
            }
            return gdcoTableTicketDictionary;
        }

        public void InsertTicket(string projectId, string gdcoTicket, string source)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(gdcoTicket))
            {
                return;
            }
            using (SqlConnection connection = new SqlConnection(this.GetConnectionString()))
            {
                try
                {
                    SqlCommand sqlCommand = new SqlCommand(this.InsertQuery(projectId, source), connection);
                    sqlCommand.CommandType = CommandType.Text;
                    sqlCommand.Parameters.AddWithValue("@gdcoTicket", gdcoTicket);
                    connection.Open();
                    sqlCommand.ExecuteScalar();
                }
                catch (Exception ex)
                {
                    throw new Exception("Exception in InsertTicket", ex);
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        public void UpdateTicket(long Id, string gdcoTicket)
        {
            if (string.IsNullOrEmpty(gdcoTicket))
            {
                return;
            }
            using (SqlConnection connection = new SqlConnection(this.GetConnectionString()))
            {
                try
                {
                    SqlCommand sqlCommand = new SqlCommand(this.UpdateQuery(Id), connection);
                    sqlCommand.CommandType = CommandType.Text;
                    sqlCommand.Parameters.AddWithValue("@gdcoTicket", gdcoTicket);
                    connection.Open();
                    sqlCommand.ExecuteScalar();
                }
                catch (Exception ex)
                {
                    throw new Exception("Exception in InsertTicket", ex);
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        private string GetQuery(string projectId, string source)
        {
            return "SELECT Id, ProjectId, GdcoTicket FROM [mcio_oa_db].[GdcoTicketTable] WHERE ProjectId = '" + projectId + "' AND Source = '" + source + "'";
        }

        private string GetQuery(List<string> projectIds, string source)
        {
            string projectIdStr = string.Empty;
            foreach (string projectId in projectIds)
            {
                projectIdStr = projectIdStr + "'" + projectId + "'" + ",";
            }
            projectIdStr = projectIdStr.Substring(0, projectIdStr.Length - 1);
            if (!string.IsNullOrEmpty(source))
            {
                return "SELECT Id, ProjectId, GdcoTicket FROM [mcio_oa_db].[GdcoTicketTable] WHERE ProjectId in (" + projectIdStr + ")" + "AND Source = '" + source + "'";
            }
            else
            {
                return "SELECT Id, ProjectId, GdcoTicket FROM [mcio_oa_db].[GdcoTicketTable] WHERE ProjectId in (" + projectIdStr + ")";
            }
        }

        private string InsertQuery(string projectId, string source)
        {
            //return "INSERT INTO [mcio_oa_db].[GdcoTicketTable](ProjectId, GdcoTicket, Source) VALUES('" + projectId + "', '" + gdcoTicket + "', '" + source + "')";
            return "INSERT INTO [mcio_oa_db].[GdcoTicketTable](ProjectId, GdcoTicket, Source) VALUES('" + projectId + "', @gdcoTicket, '" + source + "')";
        }

        private string UpdateQuery(long Id)
        {
            //return "UPDATE [mcio_oa_db].[GdcoTicketTable] SET GdcoTicket = '" + gdcoTicket + "', UpdatedDate = '" + DateTime.Now + "' WHERE Id = " + Id;
            return "UPDATE [mcio_oa_db].[GdcoTicketTable] SET GdcoTicket = @gdcoTicket, UpdatedDate = '" + DateTime.Now + "' WHERE Id = " + Id;
        }

        private string GetConnectionString()
        {
            return ConnectionHandler.ConnectionString;
        }
    }
}
