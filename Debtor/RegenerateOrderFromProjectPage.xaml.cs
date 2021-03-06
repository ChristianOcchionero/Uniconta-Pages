using UnicontaClient.Models;
using UnicontaClient.Pages;
using UnicontaClient.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
using Uniconta.API.Project;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.ClientTools.Util;
using Uniconta.Common;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public class RegenerateOrderFromProjectPageGridClient : CorasauDataGridClient
    {
        public override Type TableType { get { return typeof(ProjectTransClientLocal); } }
        public override bool Readonly { get { return false; } }
    }

    public partial class RegenerateOrderFromProjectPage : GridBasePage
    {
        public override string NameOfControl { get { return TabControls.RegenerateOrderFromProjectPage; } }

        Uniconta.DataModel.DebtorOrder master;

        public RegenerateOrderFromProjectPage(UnicontaBaseEntity master): base(master)
        {
            this.master = master as Uniconta.DataModel.DebtorOrder;
            InitializeComponent();
            SetRibbonControl(localMenu, dgGenerateOrder);
            dgGenerateOrder.UpdateMaster(master);
            dgGenerateOrder.api = api;
            dgGenerateOrder.BusyIndicator = busyIndicator;
            localMenu.OnItemClicked += LocalMenu_OnItemClicked;
            dgGenerateOrder.ShowTotalSummary();
        }

        public override bool IsDataChaged { get { return false; } }

        protected override void OnLayoutLoaded()
        {
            base.OnLayoutLoaded();
            //this.ProjectCol.Visible = !(master is Uniconta.DataModel.Project);
            Utility.SetupVariants(api, null, colVariant1, colVariant2, colVariant3, colVariant4, colVariant5, Variant1Name, Variant2Name, Variant3Name, Variant4Name, Variant5Name);
            dgGenerateOrder.Readonly = true;
            Utility.SetDimensionsGrid(api, cldim1, cldim2, cldim3, cldim4, cldim5);
        }

        private void LocalMenu_OnItemClicked(string ActionType)
        {
            switch (ActionType)
            {
                case "RegenerateOrder":
                    RegenerateOrderFromProjectTrans();
                    break;
                case "AppendProjTrans":
                    var defaultdate = BasePage.GetSystemDefaultDate().Date;
                    var cw = new CWInterval(DateTime.MinValue, defaultdate);
                    cw.Closing += delegate
                    {
                        if (cw.DialogResult == true)
                            LoadNotInvoiced(cw.FromDate, cw.ToDate);
                    };
                    cw.Show();
                    break;
                default:
                    gridRibbon_BaseActions(ActionType);
                    break;
            }
        }

        async void LoadNotInvoiced(DateTime fromdate, DateTime todate)
        {
            busyIndicator.IsBusy = true;

            var invApi = new InvoiceAPI(api);
            var lst = (ProjectTransClientLocal[])await invApi.GetTransNotOnOrder(master, fromdate, todate, new ProjectTransClientLocal());
            if (lst == null || lst.Length == 0)
            {
                busyIndicator.IsBusy = false;
                UtilDisplay.ShowErrorCode(ErrorCodes.NoLinesFound);
                return;
            }

            var orgList = dgGenerateOrder.ItemsSource as ICollection<ProjectTransClientLocal>;
            var newList = new List<ProjectTransClientLocal>(orgList.Count + lst.Length);
            foreach(var rec in orgList)
            {
                if (rec._SendToOrder != 0)
                    newList.Add(rec);
            }
            for (int i = 0; (i < lst.Length); i++)
            {
                var rec = lst[i];
                rec._remove = true;
                newList.Add(rec);
            }
            busyIndicator.IsBusy = false;
            dgGenerateOrder.ItemsSource = newList;
        }

        async void RegenerateOrderFromProjectTrans()
        {
            var OrderNumber = master._OrderNumber;
            List<ProjectTransClientLocal> excludedTransLst = null, includedTransLst = null;
            var transLst = dgGenerateOrder.GetVisibleRows() as IEnumerable<ProjectTransClientLocal>;
            if (transLst == null)
                return;
            foreach(var x in transLst)
            {
                if (x._remove)
                {
                    if (x._SendToOrder == OrderNumber)
                    {
                        if (excludedTransLst == null)
                            excludedTransLst = new List<ProjectTransClientLocal>();
                        excludedTransLst.Add(x);
                    }
                }
                else if (x._SendToOrder == 0)
                {
                    if (includedTransLst == null)
                        includedTransLst = new List<ProjectTransClientLocal>();
                    includedTransLst.Add(x);
                }
            }
            if (excludedTransLst == null && includedTransLst == null)
            {
                UtilDisplay.ShowErrorCode(ErrorCodes.NoLinesFound);
                return;
            }
            busyIndicator.IsBusy = true;
            var invApi = new InvoiceAPI(api);
            var result = await invApi.RegenerateOrderFromProject(master, excludedTransLst, includedTransLst);
            busyIndicator.IsBusy = false;
            UtilDisplay.ShowErrorCode(result);
            if (result == ErrorCodes.Succes)
            {
                globalEvents.OnRefresh(NameOfControl, null);
                dockCtrl?.CloseDockItem();
            }
        }
    }

    public class ProjectTransClientLocal : ProjectTransClient
    {
        [Display(Name = "Include", ResourceType = typeof(ProjectTransClientText))]
        internal bool _remove;
        public bool Check { get { return !_remove; } set { _remove = !value; } }
    }
}
