using System;
using Microsoft.SqlServer.Management.Smo;
using System.Configuration;
using System.IO;
using RefreshQADB.Email.Request;

namespace RefreshQADB
{
    class Program
    {

        #region Properties
        public static readonly string _backUpServer = ConfigurationManager.AppSettings["BackUpServer"];
        public static readonly string _backUpDatabase = ConfigurationManager.AppSettings["BackUpDatabase"];
        public static readonly string _backUpUsername = ConfigurationManager.AppSettings["BackUpUsername"];
        public static readonly string _backUpPassword = ConfigurationManager.AppSettings["BackUpPassword"];
        public static readonly string _backUpPath = ConfigurationManager.AppSettings["BackUpPath"];
        public static readonly string _moveBackUpSourcePath = ConfigurationManager.AppSettings["MoveBackUpSourcePath"];
        public static readonly string _moveBackUpDestinationPath = ConfigurationManager.AppSettings["MoveBackUpDestinationPath"];
        public static readonly string _backupFileName = string.Format("{0}Backup-{1}.bak", _backUpDatabase, DateTime.Now.ToString("MMM-dd-yyyy"));
        public static readonly bool _isLocalHost = Convert.ToBoolean(ConfigurationManager.AppSettings["islocalHost"]);
        public static readonly string _restoreServer = ConfigurationManager.AppSettings["RestoreServer"];
        public static readonly string _restoreDatabase = ConfigurationManager.AppSettings["RestoreDatabase"];
        public static readonly string _restoreUsername = ConfigurationManager.AppSettings["RestoreUsername"];
        public static readonly string _restorePassword = ConfigurationManager.AppSettings["RestorePassword"];
        public static readonly string _restorePath = ConfigurationManager.AppSettings["RestorePath"];
        public static readonly string[] _alterUsersWithRole = ConfigurationManager.AppSettings["AlterUsers"].Split(',');
        public static readonly string[] _createUsersWithRole = ConfigurationManager.AppSettings["CreateUsers"].Split(',');
        public static readonly string _restoreLogicalDbName = ConfigurationManager.AppSettings["RestoreLogicalDbName"];
        public static readonly string _restorePhysicalDbPath = ConfigurationManager.AppSettings["RestorePhysicalDbPath"];
        public static readonly string _restoreLogicalLogName = ConfigurationManager.AppSettings["RestoreLogicalLogName"];
        public static readonly string _restorePhysicalLogPath = ConfigurationManager.AppSettings["RestorePhysicalLogPath"];
        //public static readonly bool _backUpServerIsWindowsAuthentication = Convert.ToBoolean(ConfigurationManager.AppSettings["BackUpServerIsWindowsAuthentication"]);

        #endregion

        /// <summary>
        /// This main method for the execution.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            try
            {
                DoTheWork();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database refresh aborted with errors.");
                Console.WriteLine("Oops error occurred \n Error Message {0}", ex.GetBaseException());

            }
            finally
            {
                Console.WriteLine("Press any key to terminate.");
                Console.ReadKey();
            }
        }

        #region Private Methods

        /// <summary>
        ///This method start the work.
        /// </summary>
        private static void DoTheWork()
        {
            IEmail email = new Email.Request.Email();
            try
            {
                // Step 1 take backup of the database 
                GetBackUp();

                //Step 2 copy the backup file to the destination restore server
                MoveBackpFile();

                //Step 3 Restore the database at the destination server
                RestoreDatabase();

                //Step 4 Restore the destination server users for the database.
                RestoreUsers();

                //Step 5 Sending mail 
                string subject = $"{_restoreDatabase} database refresh is succeeded.";
                string message = $"{_restoreDatabase} database restore on {_restoreServer} server is completed successfully.";
                email.SendMail(subject, message);
            }
            catch (Exception ex)
            {
                string subject = $"{_restoreDatabase} database refresh is failed.";
                string message = $"{_restoreDatabase} database restore on {_restoreServer} server is failed below is the detail. \n <b>Message :</b> {ex.Message} \n<b>Stack Trace :</b> {ex.StackTrace}";
                email.SendMail(subject, message);
                throw;
            }
            finally
            {
                email = null;
            }
        }

        /// <summary>
        /// Get server connection is used to get the connection to the server and return server instance.
        /// </summary>
        /// <param name="islocal"></param>
        /// <param name="serverName"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns>server</returns>
        private static Server GetServerConnection(bool islocal, string serverName, string username, string password)
        {
            Server server;
            if (!islocal)
            {
                //using sql server authentication
                server = new Server(serverName);
                server.ConnectionContext.LoginSecure = false;
                server.ConnectionContext.Login = username;
                server.ConnectionContext.Password = password;
                return server;
            }
            else
            {
                //using local windows athuentication
                server = new Server("localhost");
                return server;
            }
        }

        /// <summary>
        /// Get Backup is used to start the back of the database.
        /// </summary>
        private static void GetBackUp()
        {
            Server server;
            try
            {
                Console.WriteLine("Database {0} backup started please wait... ", _backUpDatabase);
                server = GetServerConnection(false, _backUpServer, _backUpUsername, _backUpPassword);
                Backup backupMgr = new Backup();
                //backupMgr.Devices.AddDevice(filePath, DeviceType.File);
                //backupMgr.Database = databaseName;
                backupMgr.Devices.AddDevice(
                    string.Format("{0}{1}",
                    _backUpPath, _backupFileName)
                    , DeviceType.File);
                backupMgr.Database = _backUpDatabase;
                backupMgr.Action = BackupActionType.Database;
                backupMgr.CompressionOption = BackupCompressionOptions.On;
                backupMgr.SqlBackup(server);
                Console.WriteLine("Database {0} backup completed ", _backUpDatabase);
                server = null;
            }

            catch (Exception ex)
            {
                Console.WriteLine("Database {0} backup aborted with errors.", _backUpDatabase);
                Console.WriteLine("Oops error occurred during backup error message - {0}", ex.GetBaseException());
                throw;
            }
            finally
            {
                server = null;
                GC.Collect();
            }
        }

        /// <summary>
        /// Move Back up file from the backup server to the restore server.
        /// </summary>
        private static void MoveBackpFile()
        {

            Console.WriteLine("Moving backup file please wait...");
            var source = @_moveBackUpSourcePath + _backupFileName;
            var destinaton = @_moveBackUpDestinationPath + _backupFileName;
            if (File.Exists(source))
            {
                File.Copy(source, destinaton, true);
                Console.WriteLine("Moving back file is completed.");
            }

            else
            {
                Console.WriteLine("Moving backup file is aborted with errors.");
                throw new FileNotFoundException("File not found", _backupFileName);
            }

        }

        /// <summary>
        /// This method is used to restore the database at the destnation server.
        /// </summary>
        private static void RestoreDatabase()
        {
            Server server;
            try
            {
                Console.WriteLine("Restore Database {0} started please wait...", _restoreDatabase);
                var restoreDbFilePath = string.Format("{0}{1}", _restorePath, _backupFileName);
                if (!string.IsNullOrEmpty(_restoreUsername) && !string.IsNullOrEmpty(_restorePassword))
                {
                    server = GetServerConnection(false, _restoreServer, _restoreUsername, _restorePassword);
                }
                else
                {
                    server = GetServerConnection(true, "", "", "");
                }
                Database restoreDatabase = server.Databases[_restoreDatabase];
                if (restoreDatabase != null)
                    server.KillAllProcesses(restoreDatabase.Name);//detach

                // Generate new FilePath for both Files.
                string fileMdf = Path.Combine(_restorePhysicalDbPath, $"{_restoreDatabase}_data.mdf");
                string fileLdf = Path.Combine(_restorePhysicalLogPath, $"{_restoreDatabase}_log.ldf");
                //RelocateFile relocateMdf = new RelocateFile($"{_backUpDatabase}", fileMdf);
                //RelocateFile relocateLdf = new RelocateFile($"{_backUpDatabase}_log", fileLdf);
                RelocateFile relocateMdf = new RelocateFile(_restoreLogicalDbName, fileMdf);
                RelocateFile relocateLdf = new RelocateFile(_restoreLogicalLogName, fileLdf);
                //string logicalDbName, logicalLogName, physicalDbName, physicalLogName;
                var restore = new Restore();
                restore.Devices.AddDevice(restoreDbFilePath, DeviceType.File);
                //DataTable dt = restore.ReadFileList(server);
                //foreach (DataRow row in dt.Rows)
                //{
                //    if (row["Type"].ToString() == "D")
                //    {
                //        logicalDbName = row["LogicalName"].ToString();
                //        physicalDbName = row["PhyscialName"].ToString();
                //    }
                //    if (row["Type"].ToString() == "L")
                //    {
                //        logicalLogName = row["LogicalName"].ToString();
                //        physicalLogName = row["PhyscialName"].ToString();
                //    }
                //}

                restore.Database = _restoreDatabase;
                restore.Action = RestoreActionType.Database;
                restore.RelocateFiles.Add(relocateMdf);
                restore.RelocateFiles.Add(relocateLdf);
                restore.ReplaceDatabase = true;
                restore.NoRecovery = false;
                restore.PercentCompleteNotification = 10;
                restore.SqlRestore(server);
                Console.WriteLine("Restore Database {0} completed successfully.", _restoreDatabase);
                server.Refresh();
                server = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Oops error occurred during restore error message - {0}", ex.GetBaseException());
                throw;
            }
            finally
            {
                server = null;
                GC.Collect();
            }

        }

        private static void DeleteBackUpFile()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// this method is used to restore the users and their permissions.
        /// </summary>
        private static void RestoreUsers()
        {
            try
            {
                Server server;
                if (!string.IsNullOrEmpty(_restoreUsername) && !string.IsNullOrEmpty(_restorePassword))
                {
                    server = GetServerConnection(false, _restoreServer, _restoreUsername, _restorePassword);
                }
                else
                {
                    server = GetServerConnection(true, "", "", "");
                }
                if (server != null)
                {
                    Console.WriteLine("Updating user roles please wait...");
                    var restoredDatabase = server.Databases[_restoreDatabase];
                    if (restoredDatabase != null)
                    {
                        UserCollection userCollection = restoredDatabase.Users;
                        foreach (var createUser in _createUsersWithRole)
                        {
                            string[] newUserInfo = createUser.Split('-');
                            User user = new User(restoredDatabase, newUserInfo[0]);
                            user.Login = newUserInfo[0];
                            user.Create();
                            user.AddToRole(newUserInfo[1]);
                            user.Alter();
                        }
                        foreach (var userWithRole in _alterUsersWithRole)
                        {
                            //var login = new Login(server,)
                            string[] userInfo = userWithRole.Split('-');
                            var user = userCollection[userInfo[0]];
                            user.Drop();
                            User newUser = new User(restoredDatabase, userInfo[0]);
                            newUser.Login = userInfo[0];
                            newUser.Create();
                            //user.Login = userInfo[0];
                            //user.Alter();
                            newUser.AddToRole(userInfo[1]);
                            newUser.Alter();
                        }
                        Console.WriteLine("Updating user roles completed.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Updating user roles aborted with errors - {0}", ex.GetBaseException());
                throw;
            }


        }


        #endregion
    }
}
