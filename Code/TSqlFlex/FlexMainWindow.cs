﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TSqlFlex.Core;

namespace TSqlFlex
{
    public partial class FlexMainWindow : UserControl
    {

        private SqlConnectionStringBuilder connStringBuilder = null;

        public FlexMainWindow()
        {
            InitializeComponent();
        }
    
        public void SetConnection(SqlConnectionStringBuilder theConnectionStringBuilder)
        {
            connStringBuilder = theConnectionStringBuilder;
            SetConnectionText();
        }

        private void SetConnectionText()
        {
            if (Utils.IsValidConnectionStringBuilder(connStringBuilder))
            {
                lblConnectionInfo.Text = "Instance: " + connStringBuilder.DataSource + ", DB: " + connStringBuilder.InitialCatalog;
            } else {
                lblConnectionInfo.Text = "Not connected.";
            }
        }

        private void cmdRunNRollback_Click(object sender, EventArgs e)
        {
            if (Utils.IsValidConnectionStringBuilder(connStringBuilder))
            {
                using (SqlConnection conn = new SqlConnection(connStringBuilder.ConnectionString))
                {
                    conn.Open();

                    FlexResultSet resultSet = FlexResultSet.AnalyzeResultWithRollback(conn, txtSqlInput.Text);

                    var sb = new StringBuilder();
                    for (int i = 0; i < resultSet.results.Count; i++)
                    {
                        sb.AppendLine(resultSet.ScriptResultAsCreateTable(i, "#Result" + (i + 1).ToString()));
                        sb.Append("\r\n");
                    }

                    txtOutput.Text = sb.ToString();
                }
            }
            else
            {
                MessageBox.Show("Select the target database in the object tree first.");
            }
        }

        private void btnCopyToClipboard_Click(object sender, EventArgs e)
        {
            if (txtOutput.Text.Length > 0)
            {
                Clipboard.SetText(txtOutput.Text);
            }
        }

        private void btnCopyToNewWindow_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Not yet implemented.");
        }

    }
}