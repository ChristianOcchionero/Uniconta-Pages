using UnicontaClient.Models;
using UnicontaClient.Pages;
using System;
using System.Collections;
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
using Uniconta.ClientTools.Controls;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.ClientTools.Util;
using Uniconta.Common;
using Uniconta.DataModel;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public class DebtorOrderProjectLineLocal : DebtorOrderProjectLineClient
    {
        internal bool InsidePropChange;
        public double costPct, salesPct, costAmount, salesAmount;

        public void SetCost(double cost)
        {
            if (cost != 0d)
                this.CostPrice = Math.Round(cost * (1d + costPct / 100d) + costAmount, 2);
        }
        public void SetSales(double sales)
        {
            if (sales != 0d)
                this.SalesPrice = Math.Round(sales * (1d + salesPct / 100d) + salesAmount, 2);
        }

        public PrCategoryCacheFilter PrCategorySource { get; internal set; }
    }

    public class DebtorOrderProjectLineGrid : CorasauDataGridClient
    {
        public override Type TableType { get { return typeof(DebtorOrderProjectLineLocal); } }
        public override IComparer GridSorting { get { return new DCProjectOrderLineSort(); } }
        public override string LineNumberProperty { get { return "_LineNumber"; } }
        public override bool AllowSort { get { return false; } }
        public override bool Readonly { get { return false; } }
        public override bool IsAutoSave { get { return true; } }

        public override bool AddRowOnPageDown()
        {
            var selectedItem = (DebtorOrderProjectLineLocal)this.SelectedItem;
            if (selectedItem == null || selectedItem._Project == null || (selectedItem._Qty == 0d && selectedItem._SalesPrice == 0d && selectedItem._CostPrice == 0d))
                return false;
            return true;
        }

        public override void SetDefaultValues(UnicontaBaseEntity dataEntity, int selectedIndex)
        {
            var newRow = (DebtorOrderProjectLineLocal)dataEntity;
            var header = this.masterRecord as Uniconta.DataModel.DebtorOrder;
            if (header != null)
            {
                newRow.SetMaster(header);
                newRow._Dim1 = header._Dim1;
                newRow._Dim2 = header._Dim2;
                newRow._Dim3 = header._Dim3;
                newRow._Dim4 = header._Dim4;
                newRow._Dim5 = header._Dim5;
                newRow._Employee = header._Employee;
                newRow._TransType = header._TransType;
            }

            var lst = (IList)this.ItemsSource;
            if (lst == null || lst.Count == 0)
            {
                newRow._Date = BasePage.GetSystemDefaultDate().Date;
            }
            else
            {
                DebtorOrderProjectLineLocal last = null;
                DebtorOrderProjectLineLocal Cur = null;
                int n = -1;
                DateTime LastDateTime = DateTime.MinValue;
                var castItem = lst as IEnumerable<DebtorOrderProjectLineLocal>;
                foreach (var journalLine in castItem)
                {
                    if (journalLine._Date != DateTime.MinValue && Cur == null)
                        LastDateTime = journalLine._Date;
                    n++;
                    if (n == selectedIndex)
                        Cur = journalLine;
                    last = journalLine;
                }
                if (Cur == null)
                    Cur = last;

                newRow._Date = LastDateTime != DateTime.MinValue ? LastDateTime : BasePage.GetSystemDefaultDate().Date;
                newRow._Project = last._Project;
                newRow._PrCategory = last._PrCategory;
                newRow.PrCategorySource = last.PrCategorySource;
            }
        }
    }

    public partial class DebtorOrderProjectLinePage : GridBasePage
    {
        public override string NameOfControl { get { return TabControls.DebtorOrderProjectLinePage; } }

        SQLCache ItemsCache, ProjectCache, DebtorCache, CategoryCache, EmployeeCache, PrStandardCache;
        Dictionary<string, Uniconta.API.DebtorCreditor.FindPrices> dictPriceLookup;
        DebtorOrder debtorOrder;
        public DebtorOrderProjectLinePage(UnicontaBaseEntity master) : base(master)
        {
            InitializeComponent();
            localMenu.dataGrid = dgDebtorOrderProjectLineGrid;
            SetRibbonControl(localMenu, dgDebtorOrderProjectLineGrid);
            dgDebtorOrderProjectLineGrid.api = api;
            dgDebtorOrderProjectLineGrid.UpdateMaster(master);
            dgDebtorOrderProjectLineGrid.BusyIndicator = busyIndicator;
            localMenu.OnItemClicked += localMenu_OnItemClicked;
            dgDebtorOrderProjectLineGrid.View.DataControl.CurrentItemChanged += DataControl_CurrentItemChanged;
            dictPriceLookup = new Dictionary<string, Uniconta.API.DebtorCreditor.FindPrices>();
            debtorOrder = master as DebtorOrder;
        }

        protected override void OnLayoutLoaded()
        {
            base.OnLayoutLoaded();
            UnicontaClient.Utilities.Utility.SetDimensionsGrid(api, coldim1, coldim2, coldim3, coldim4, coldim5);
        }

        public async override Task InitQuery()
        {
            await dgDebtorOrderProjectLineGrid.Filter(null);
            if (debtorOrder != null)
                await api.Read(debtorOrder);
            RecalculateAmount();
        }

        private void localMenu_OnItemClicked(string ActionType)
        {
            switch (ActionType)
            {
                case "AddRow":
                    dgDebtorOrderProjectLineGrid.AddRow();
                    break;
                case "CopyRow":
                    dgDebtorOrderProjectLineGrid.CopyRow();
                    break;
                case "SaveGrid":
                    saveGrid();
                    break;
                case "DeleteRow":
                    dgDebtorOrderProjectLineGrid.DeleteRow();
                    break;
                case "UpdatePrices":
                    UpdatePrices();
                    break;
                default:
                    gridRibbon_BaseActions(ActionType);
                    break;
            }
            RecalculateAmount();
        }

        private void DataControl_CurrentItemChanged(object sender, DevExpress.Xpf.Grid.CurrentItemChangedEventArgs e)
        {
            DebtorOrderProjectLineLocal oldselectedItem = e.OldItem as DebtorOrderProjectLineLocal;
            if (oldselectedItem != null)
                oldselectedItem.PropertyChanged -= SelectedItem_PropertyChanged;

            DebtorOrderProjectLineLocal selectedItem = e.NewItem as DebtorOrderProjectLineLocal;
            if (selectedItem != null)
            {
                selectedItem.InsidePropChange = false;
                selectedItem.PropertyChanged += SelectedItem_PropertyChanged;
            }
        }

        private void SelectedItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var rec = (DebtorOrderProjectLineLocal)sender;
            switch (e.PropertyName)
            {
                case "Item":
                    if (!rec.InsidePropChange)
                    {
                        rec.InsidePropChange = true;
                        SetItem(rec);
                        getCostAndSales(rec);
                        rec.InsidePropChange = false;
                    }
                    break;
                case "Project":
                    var pro = (Uniconta.DataModel.Project)ProjectCache.Get(rec._Project);
                    if (pro != null)
                    {
                        if (pro._Dim1 != null) rec.Dimension1 = pro._Dim1;
                        if (pro._Dim2 != null) rec.Dimension2 = pro._Dim2;
                        if (pro._Dim3 != null) rec.Dimension3 = pro._Dim3;
                        if (pro._Dim4 != null) rec.Dimension4 = pro._Dim4;
                        if (pro._Dim5 != null) rec.Dimension5 = pro._Dim5;
                        getCostAndSales(rec);
                    }
                    break;
                case "PrCategory":
                    getCostAndSales(rec);
                    break;
                case "Qty":
                    UpdatePrice(rec);
                    break;
            }
            RecalculateAmount();
        }

        void SetItem(DebtorOrderProjectLineLocal rec)
        {
            var item = (InvItem)ItemsCache.Get(rec._Item);
            if (item == null)
                return;

            //  SetPriceLookup(rec)?.SetPriceFromItem(rec, item);

            if (item._Dim1 != null) rec.Dimension1 = item._Dim1;
            if (item._Dim2 != null) rec.Dimension2 = item._Dim2;
            if (item._Dim3 != null) rec.Dimension3 = item._Dim3;
            if (item._Dim4 != null) rec.Dimension4 = item._Dim4;
            if (item._Dim5 != null) rec.Dimension5 = item._Dim5;
            if (item._PrCategory != null)
                rec.PrCategory = item._PrCategory;
        }

        async void getCostAndSales(DebtorOrderProjectLineLocal rec)
        {
            var project = rec._Project;
            if (project == null)
                return;
            var proj = (Uniconta.DataModel.Project)ProjectCache.Get(project);
            var Categories = proj.Categories ?? await proj.LoadCategories(api);

            rec.costPct = 0d; rec.salesPct = 0d; rec.costAmount = 0d; rec.salesAmount = 0d;

            var Category = rec._PrCategory;
            var projCat = (from ct in Categories where ct._PrCategory == Category select ct).FirstOrDefault();
            if (projCat != null)
            {
                rec.costPct = projCat._CostPctCharge;
                rec.salesPct = projCat._SalesPctCharge;
                rec.costAmount = projCat._CostAmountCharge;
                rec.salesAmount = projCat._SalesAmountCharge;
            }
            else
            {
                var prstd = (PrStandard)PrStandardCache.Get(proj._PrStandard);
                if (prstd == null)
                    return;
                var PrCategories = prstd.Categories ?? await prstd.LoadCategories(api);

                var prCat = (from ct in PrCategories where ct._PrCategory == Category select ct).FirstOrDefault();
                if (prCat != null)
                {
                    rec.costPct = prCat._CostPctCharge;
                    rec.salesPct = prCat._SalesPctCharge;
                    rec.costAmount = prCat._CostAmountCharge;
                    rec.salesAmount = prCat._SalesAmountCharge;
                }
            }
            RecalculateAmount();
        }

        void UpdatePrices()
        {
            var source = dgDebtorOrderProjectLineGrid.GetVisibleRows() as IEnumerable<DebtorOrderProjectLineLocal>;
            foreach (var rec in source)
            {
                dgDebtorOrderProjectLineGrid.SetLoadedRow(rec);
                var pro = (Uniconta.DataModel.Project)ProjectCache.Get(rec._Project);
                if (pro != null)
                {
                    if (pro._Dim1 != null) rec.Dimension1 = pro._Dim1;
                    if (pro._Dim2 != null) rec.Dimension2 = pro._Dim2;
                    if (pro._Dim3 != null) rec.Dimension3 = pro._Dim3;
                    if (pro._Dim4 != null) rec.Dimension4 = pro._Dim4;
                    if (pro._Dim5 != null) rec.Dimension5 = pro._Dim5;
                }

                rec.InsidePropChange = true;
                if (rec._Item != null)
                {
                    var cat = rec._PrCategory;
                    SetItem(rec);
                    if (cat != null)
                        rec.PrCategory = cat;
                }
                else
                    getCostAndSales(rec);

                dgDebtorOrderProjectLineGrid.SetModifiedRow(rec);
                rec.InsidePropChange = false;
            }
        }

        void UpdatePrice(DebtorOrderProjectLineLocal rec)
        {
            var priceLookup = SetPriceLookup(rec);
            //if (priceLookup != null && priceLookup.UseCustomerPrices)
            //    priceLookup.GetCustomerPrice(rec, false);
        }

        Uniconta.API.DebtorCreditor.FindPrices SetPriceLookup(DebtorOrderProjectLineLocal rec)
        {
            var proj = (Uniconta.DataModel.Project)ProjectCache.Get(rec._Project);
            if (proj != null)
            {
                if (dictPriceLookup.ContainsKey(proj._DCAccount))
                    return dictPriceLookup[proj._DCAccount];

                var order = new DebtorOrder() { _DCAccount = proj._DCAccount };
                var priceLookup = new Uniconta.API.DebtorCreditor.FindPrices(order, api);
                dictPriceLookup.Add(proj._DCAccount, priceLookup);
                return priceLookup;
            }
            return null;
        }

        protected override async void LoadCacheInBackGround()
        {
            var api = this.api;
            ProjectCache = api.GetCache(typeof(Uniconta.DataModel.Project)) ?? await api.LoadCache(typeof(Uniconta.DataModel.Project)).ConfigureAwait(false);
            ItemsCache = api.GetCache(typeof(Uniconta.DataModel.InvItem)) ?? await api.LoadCache(typeof(Uniconta.DataModel.InvItem)).ConfigureAwait(false);
            DebtorCache = api.GetCache(typeof(Uniconta.DataModel.Debtor)) ?? await api.LoadCache(typeof(Uniconta.DataModel.Debtor)).ConfigureAwait(false);
            CategoryCache = api.GetCache(typeof(Uniconta.DataModel.PrCategory)) ?? await api.LoadCache(typeof(Uniconta.DataModel.PrCategory)).ConfigureAwait(false);
            EmployeeCache = api.GetCache(typeof(Uniconta.DataModel.Employee)) ?? await api.LoadCache(typeof(Uniconta.DataModel.Employee)).ConfigureAwait(false);
            PrStandardCache = api.GetCache(typeof(Uniconta.DataModel.PrStandard)) ?? await api.LoadCache(typeof(Uniconta.DataModel.PrStandard)).ConfigureAwait(false);
        }

        void RecalculateAmount()
        {
            var lst = dgDebtorOrderProjectLineGrid.ItemsSource as List<DebtorOrderProjectLineLocal>;
            if (lst == null)
                return;
            double adjustment = debtorOrder._OrderTotal - debtorOrder._ProjectTotal;
            double Amountsum = lst.Sum(x=>x._SalesAmount);
            double difference = adjustment - Amountsum;
            RibbonBase rb = (RibbonBase)localMenu.DataContext;
            var groups = UtilDisplay.GetMenuCommandsByStatus(rb, true);
            var adjust = Uniconta.ClientTools.Localization.lookup("Adjustment");
            var strTotal = Uniconta.ClientTools.Localization.lookup("Total");
            var diff = Uniconta.ClientTools.Localization.lookup("Diff");
            foreach (var grp in groups)
            {
                if (grp.Caption == adjust)
                    grp.StatusValue = adjustment.ToString("N2");
                if (grp.Caption == strTotal)
                    grp.StatusValue = Amountsum.ToString("N2");
                if (grp.Caption == diff)
                    grp.StatusValue = difference.ToString("N2");
            }
        }
    }
}
