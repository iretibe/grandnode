﻿using Grand.Core;
using Grand.Core.Domain.Directory;
using Grand.Framework.Kendoui;
using Grand.Framework.Mvc;
using Grand.Framework.Mvc.Filters;
using Grand.Services.Common;
using Grand.Services.Directory;
using Grand.Services.ExportImport;
using Grand.Services.Localization;
using Grand.Services.Security;
using Grand.Services.Stores;
using Grand.Web.Areas.Admin.Extensions;
using Grand.Web.Areas.Admin.Models.Directory;
using Grand.Web.Areas.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grand.Web.Areas.Admin.Controllers
{
    public partial class CountryController : BaseAdminController
	{
		#region Fields

        private readonly ICountryService _countryService;
        private readonly ICountryViewModelService _countryViewModelService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ILocalizationService _localizationService;
	    private readonly IAddressService _addressService;
        private readonly IPermissionService _permissionService;
        private readonly ILanguageService _languageService;
        private readonly IStoreService _storeService;
        private readonly IExportManager _exportManager;
        private readonly IImportManager _importManager;

	    #endregion

		#region Constructors

        public CountryController(ICountryService countryService,
            ICountryViewModelService countryViewModelService,
            IStateProvinceService stateProvinceService, 
            ILocalizationService localizationService,
            IAddressService addressService, 
            IPermissionService permissionService,
            ILanguageService languageService,
            IStoreService storeService,
            IExportManager exportManager,
            IImportManager importManager)
		{
            this._countryService = countryService;
            this._countryViewModelService = countryViewModelService;
            this._stateProvinceService = stateProvinceService;
            this._localizationService = localizationService;
            this._addressService = addressService;
            this._permissionService = permissionService;
            this._languageService = languageService;
            this._storeService = storeService;
            this._exportManager = exportManager;
            this._importManager = importManager;
		}

		#endregion 

        #region Countries

        public IActionResult Index()
        {
            return RedirectToAction("List");
        }

        public IActionResult List()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            return View();
        }

        [HttpPost]
        public IActionResult CountryList(DataSourceRequest command)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var countries = _countryService.GetAllCountries(showHidden: true);
            var gridModel = new DataSourceResult
            {
                Data = countries.Select(x => x.ToModel()),
                Total = countries.Count
            };

            return Json(gridModel);
        }
        
        public IActionResult Create()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var model = _countryViewModelService.PrepareCountryModel();
            //locales
            AddLocales(_languageService, model.Locales);
            //Stores
            model.PrepareStoresMappingModel(null, false, _storeService);
            
            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public IActionResult Create(CountryModel model, bool continueEditing)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            if (ModelState.IsValid)
            {
                var country = _countryViewModelService.InsertCountryModel(model);
                SuccessNotification(_localizationService.GetResource("Admin.Configuration.Countries.Added"));
                return continueEditing ? RedirectToAction("Edit", new { id = country.Id }) : RedirectToAction("List");
            }
            //If we got this far, something failed, redisplay form
            //Stores
            model.PrepareStoresMappingModel(null, true, _storeService);

            return View(model);
        }

        public IActionResult Edit(string id)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var country = _countryService.GetCountryById(id);
            if (country == null)
                //No country found with the specified id
                return RedirectToAction("List");

            var model = country.ToModel();
            //locales
            AddLocales(_languageService, model.Locales, (locale, languageId) =>
            {
                locale.Name = country.GetLocalized(x => x.Name, languageId, false, false);
            });
            //Stores
            model.PrepareStoresMappingModel(country, false, _storeService);

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public IActionResult Edit(CountryModel model, bool continueEditing)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var country = _countryService.GetCountryById(model.Id);
            if (country == null)
                //No country found with the specified id
                return RedirectToAction("List");

            if (ModelState.IsValid)
            {
                country = _countryViewModelService.UpdateCountryModel(country, model);
                SuccessNotification(_localizationService.GetResource("Admin.Configuration.Countries.Updated"));
                if (continueEditing)
                {
                    //selected tab
                    SaveSelectedTabIndex();

                    return RedirectToAction("Edit", new {id = country.Id});
                }
                return RedirectToAction("List");
            }
            //If we got this far, something failed, redisplay form
            //Stores
            model.PrepareStoresMappingModel(country, true, _storeService);

            return View(model);
        }

        [HttpPost]
        public IActionResult Delete(string id)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var country = _countryService.GetCountryById(id);
            if (country == null)
                //No country found with the specified id
                return RedirectToAction("List");

            try
            {
                if (_addressService.GetAddressTotalByCountryId(country.Id) > 0)
                    throw new GrandException("The country can't be deleted. It has associated addresses");
                if (ModelState.IsValid)
                {
                    _countryService.DeleteCountry(country);
                    SuccessNotification(_localizationService.GetResource("Admin.Configuration.Countries.Deleted"));
                    return RedirectToAction("List");
                }
                ErrorNotification(ModelState);
                return RedirectToAction("Edit", new { id = id });
            }
            catch (Exception exc)
            {
                ErrorNotification(exc);
                return RedirectToAction("Edit", new { id = country.Id });
            }
        }

        [HttpPost]
        public IActionResult PublishSelected(ICollection<string> selectedIds)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            if (selectedIds != null)
            {
                var countries = _countryService.GetCountriesByIds(selectedIds.ToArray());
                foreach (var country in countries)
                {
                    country.Published = true;
                    _countryService.UpdateCountry(country);
                }
            }

            return Json(new { Result = true });
        }
        [HttpPost]
        public IActionResult UnpublishSelected(ICollection<string> selectedIds)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            if (selectedIds != null)
            {
                var countries = _countryService.GetCountriesByIds(selectedIds.ToArray());
                foreach (var country in countries)
                {
                    country.Published = false;
                    _countryService.UpdateCountry(country);
                }
            }
            return Json(new { Result = true });
        }

        #endregion

        #region States / provinces

        [HttpPost]
        public IActionResult States(string countryId, DataSourceRequest command)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var states = _stateProvinceService.GetStateProvincesByCountryId(countryId, showHidden: true);

            var gridModel = new DataSourceResult
            {
                Data = states.Select(x => x.ToModel()),
                Total = states.Count
            };
            return Json(gridModel);
        }

        //create
        public IActionResult StateCreatePopup(string countryId)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var model = _countryViewModelService.PrepareStateProvinceModel(countryId);
            //locales
            AddLocales(_languageService, model.Locales);
            return View(model);
        }

        [HttpPost]
        public IActionResult StateCreatePopup(StateProvinceModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var country = _countryService.GetCountryById(model.CountryId);
            if (country == null)
                //No country found with the specified id
                return RedirectToAction("List");

            if (ModelState.IsValid)
            {
                var sp = _countryViewModelService.InsertStateProvinceModel(model);
                ViewBag.RefreshPage = true;
                return View(model);
            }

            //If we got this far, something failed, redisplay form
            return View(model);
        }

        //edit
        public IActionResult StateEditPopup(string id)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var sp = _stateProvinceService.GetStateProvinceById(id);
            if (sp == null)
                //No state found with the specified id
                return RedirectToAction("List");

            var model = sp.ToModel();
            //locales
            AddLocales(_languageService, model.Locales, (locale, languageId) =>
            {
                locale.Name = sp.GetLocalized(x => x.Name, languageId, false, false);
            });

            return View(model);
        }

        [HttpPost]
        public IActionResult StateEditPopup(StateProvinceModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var sp = _stateProvinceService.GetStateProvinceById(model.Id);
            if (sp == null)
                //No state found with the specified id
                return RedirectToAction("List");

            if (ModelState.IsValid)
            {
                sp = _countryViewModelService.UpdateStateProvinceModel(sp, model);
                ViewBag.RefreshPage = true;
                return View(model);
            }

            //If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpPost]
        public IActionResult StateDelete(string id)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            var state = _stateProvinceService.GetStateProvinceById(id);
            if (state == null)
                throw new ArgumentException("No state found with the specified id");

            if (_addressService.GetAddressTotalByStateProvinceId(state.Id) > 0)
            {
                return Json(new DataSourceResult { Errors = _localizationService.GetResource("Admin.Configuration.Countries.States.CantDeleteWithAddresses") });
            }
            if (ModelState.IsValid)
            {
                _stateProvinceService.DeleteStateProvince(state);
                return new NullJsonResult();
            }
            return ErrorForKendoGridJson(ModelState);
        }

        [AcceptVerbs("Get")]
        public IActionResult GetStatesByCountryId(string countryId,
            bool? addSelectStateItem, bool? addAsterisk)
        {
            // This action method gets called via an ajax request
            if (String.IsNullOrEmpty(countryId))
                return Json(new List<dynamic>() { new { id = "", name = _localizationService.GetResource("Address.SelectState") } });

            var country = _countryService.GetCountryById(countryId);
            var states = country != null ? _stateProvinceService.GetStateProvincesByCountryId(country.Id, showHidden: true).ToList() : new List<StateProvince>();
            var result = (from s in states
                         select new { id = s.Id, name = s.Name }).ToList();
            if (addAsterisk.HasValue && addAsterisk.Value)
            {
                //asterisk
                result.Insert(0, new { id = "", name = "*" });
            }
            else
            {
                if (country == null)
                {
                    //country is not selected ("choose country" item)
                    if (addSelectStateItem.HasValue && addSelectStateItem.Value)
                    {
                        result.Insert(0, new { id = "", name = _localizationService.GetResource("Admin.Address.SelectState") });
                    }
                    else
                    {
                        result.Insert(0, new { id = "", name = _localizationService.GetResource("Admin.Address.OtherNonUS") });
                    }
                }
                else
                {
                    //some country is selected
                    if (result.Count == 0)
                    {
                        //country does not have states
                        result.Insert(0, new { id = "", name = _localizationService.GetResource("Admin.Address.OtherNonUS") });
                    }
                    else
                    {
                        //country has some states
                        if (addSelectStateItem.HasValue && addSelectStateItem.Value)
                        {
                            result.Insert(0, new { id = "", name = _localizationService.GetResource("Admin.Address.SelectState") });
                        }
                    }
                }
            }
            return Json(result);
        }

        #endregion

        #region Export / import

        public IActionResult ExportCsv()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            string fileName = String.Format("states_{0}_{1}.txt", DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"), CommonHelper.GenerateRandomDigitCode(4));

            var states = _stateProvinceService.GetStateProvinces(true);
            string result = _exportManager.ExportStatesToTxt(states);

            return File(Encoding.UTF8.GetBytes(result), "text/csv", fileName);
        }

        [HttpPost]
        public IActionResult ImportCsv(IFormFile importcsvfile)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCountries))
                return AccessDeniedView();

            try
            {
                if (importcsvfile != null && importcsvfile.Length > 0)
                {
                    int count = _importManager.ImportStatesFromTxt(importcsvfile.OpenReadStream());
                    SuccessNotification(String.Format(_localizationService.GetResource("Admin.Configuration.Countries.ImportSuccess"), count));
                    return RedirectToAction("List");
                }
                ErrorNotification(_localizationService.GetResource("Admin.Common.UploadFile"));
                return RedirectToAction("List");
            }
            catch (Exception exc)
            {
                ErrorNotification(exc);
                return RedirectToAction("List");
            }
        }

        #endregion
    }
}
