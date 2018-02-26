
var assetsStorage = angular.module('assetsStorageApp', ['ui.bootstrap', 'remove.diacritics']);

assetsStorage.directive('fileModel', ['$parse', function ($parse) {
    return {
        restrict: 'A',
        link: function (scope, element, attrs) {
            var model = $parse(attrs.fileModel);
            var modelSetter = model.assign;

            element.bind('change', function () {
                scope.$apply(function () {
                    modelSetter(scope, element[0].files[0]);
                });
            });
        }
    };
}]);

assetsStorage.controller('CreateBranchPopup', function ($http, $uibModalInstance, removeDiacritics, datas) {
    var self = this;
    this.branchName = "";
    this.alerts = [];
    this.selectBranch = {
        model: null,
        availableOptions: datas.branches
    }

    /************ Event ************/
    // Try to create branch
    this.ok = function () {
        self.chekFields();

        if (self.alerts.length != 0)
            return;

        this.pending = true;
        parent = "";
        if (self.selectBranch.model != null) {
            parent = '&parentBranch=' + self.selectBranch.model;
        }

        // Try to create branch
        $http.put('/' + datas.account + '/' + datas.application + '/_admin/_assetsstorage/' + self.branchName + '?x-token=' + datas.xToken + parent)
            .success(function (data, status, headers, config) {
                // Check if the server encounter an errors      
                self.alerts = [];
                self.alerts.push({ type: 'success', message: 'branch created' });
                $uibModalInstance.close();
                self.pending = false;
            })
            .error(function (data, status, headers, config) {
                self.alerts = [];
                self.alerts.push({ type: 'danger', message: data });
                self.pending = false;
            });
    };

    this.checkAccent = function () {
        self.branchName = removeDiacritics.seo(this.branchName);
    }

    //Cancel
    this.cancel = function () {
        $uibModalInstance.dismiss("cancel");
    };

    /************ Check ************/
    // Check requiered fields
    this.chekFields = function () {
        // Clean all previous alert
        self.alerts = [];
        if (self.branchName == "") {
            self.alerts.push({ type: 'warning', message: 'Branch name is requiere' });
        }
    }
});

assetsStorage.controller('CreateFilePopup', function ($http, $uibModalInstance, removeDiacritics, datas) {
    var self = this;
    this.alerts = [];
    this.branchName = datas.activeBranch;
    // Models
    this.pathName = "";
    this.fileToUpload = "";
    this.pending = false;
    this.hideProgressBar = true;
    this.progressMax = 100;
    this.progressValue = 0;

    // Upload file
    this.uploadFileToUrl = function () {
        this.pending = true;
        var file = self.fileToUpload;
        var path = self.pathName;
        var fileType = "";
        this.hideProgressBar = false;
        //Check the type of file when the type is null, i set default value to application/octet-stream.
        //Doing this check prevent exception on server.        
        fileType = file.type || "application/octet-stream";
        this.pending = true;
        $http.put('/' + datas.account + '/' + datas.application + '/_admin/_assetsstorage/' + self.branchName + '/' + path + file.name + '?x-token=' + datas.xToken, file, {
            transformRequest: angular.identity,
            headers: { 'Content-Type': fileType },
            uploadEventHandlers: {
                progress: function (e) {
                    self.progressValue = Math.floor((e.loaded / e.total) * 100);
                    self.displayValue = self.progressValue + "%";
                }
            },
        }).success(function (data, status, headers, config) {
            if (data.IsFaulted) {
                self.alerts = [];
                self.alerts.push({ type: 'warning', message: data.Reason });
            }
            else {
                $uibModalInstance.close();
            }
            self.pending = false;
            self.hideProgressBar = true;

        }).error(function (data, status, headers, config) {
            self.alerts.push({ type: 'danger', message: data.Message });
            self.pending = false;
            self.hideProgressBar = true;
        });
    }

    /************ Event ************/
    this.ok = function () {
        self.chekFields();
        if (self.alerts.length != 0)
            return;

        self.uploadFileToUrl();
    };

    //Cancel
    this.cancel = function () {
        $uibModalInstance.dismiss("cancel");
    };

    /************ Check ************/
    // Check requiered fields
    this.chekFields = function () {
        // Clean all previous alert        
        self.alerts = [];
        if (self.fileToUpload == "") {
            self.alerts.push({ type: 'warning', message: "Please select a file to upload" });
        }
    }

    this.checkPath = function () {
        let checkValue = self.pathName;
        if (checkValue[checkValue.length - 1] != "/" && checkValue.length != 0) {
            self.pathName = checkValue + "/";
        }
        self.pathName = removeDiacritics.replace(self.pathName);
    }
});

assetsStorage.controller('InformationPopup', function ($uibModalInstance, datas) {
    this.title = datas.title;
    this.message = datas.message;
    this.ok = function () {
        $uibModalInstance.close();
    };
});

assetsStorage.controller('ConfirmationPopup', function ($http, $uibModalInstance, removeDiacritics, datas) {
    var self = this;
    this.title = datas.title;
    this.message = datas.message;

    /************ Event ************/
    // Try to create branch
    this.ok = function () {
        $uibModalInstance.close();
    };

    //Cancel
    this.cancel = function () {
        $uibModalInstance.dismiss("cancel");
    };
});

assetsStorage.controller('assetsStorageController', function ($http, $uibModal) {
    //App configuration
    var self = this;
    var account = window.appInfos.accountId;
    var application = window.appInfos.application;
    var xToken = window.appInfos.token;
    this.alerts = [];
    this.refreshDelay = 1000;
    //Navigation
    this.selectBranch = {
        model: null,
        availableOptions: []
    }
    this.pending = false;

    //File
    this.fileToUpload = "Default file";

    // Branch
    this.branches = "";

    //Field used for modal box
    this.modalTitle = "Default title";
    this.modalMessage = "Default message";

    //Progress bar
    this.progressValue = "";
    this.progressMax = 100;
    this.displayValue = "";
    this.hideProgressBar = true;

    /******************* File *******************/
    //Download file
    this.downloadFile = function (fileURL, fileName) {
        var branchName = self.selectBranch.model;
        self.pending = true;
        self.progressValue = 100;
        self.displayValue = "Loading";
        self.hideProgressBar = false;
        $http.get(fileURL, {
            responseType: 'blob',
            eventHandlers: {
                progress: function (e) {
                    self.progressValue = Math.floor((e.loaded / e.total) * 100);
                    self.displayValue = self.progressValue + "%";
                }
            },
        }).then(
            function (response) {
                self.alerts = [];
                self.hideProgressBar = true;
                self.OnSuccessDownload(response, fileName);
            },
            function (response) {
                self.hideProgressBar = true;
                self.OnFaildedModal(response);
            }
        );
    }

    this.removeFile = function (path, file) {
        var branchName = self.selectBranch.model;
        self.pending = true;
        $http.delete('/' + account + '/' + application + '/_admin/_assetsstorage/' + branchName + '/' + path + '?x-token=' + xToken)
            .then(
            function (response) {
                setTimeout(function () {
                    self.alerts = [];
                    self.getBranch(self.selectBranch.model);
                    self.pending = false;
                }, self.refreshDelay);
            },
            function (response) {
                self.pending = true;
                self.alerts = [];
                self.alerts.push({ type: 'danger', message: response.data || response.statusText });
                setTimeout(function () {
                    self.pending = false;
                    self.getBranch(self.selectBranch.model);
                }, self.refreshDelay);
            }
            );
    }

    /******************* Branch *******************/
    // Get all branched store in assets storage
    this.getBranch = function () {
        self.pending = true;
        $http.get('/' + account + '/' + application + '/_admin/_assetsstorage/' + self.selectBranch.model + '?x-token=' + xToken)
            .then(
            function (response) {
                self.pending = false;
                self.alerts = [];                
                self.files = response.data;
            },
            function (response) {
                self.pending = false;
                self.alerts = [];
                self.alerts.push({ type: 'danger', message: response.data || response.statusText });
            }
            );
    }

    // Get all branched store in assets storage
    this.getBranches = function () {
        self.pending = true;
        $http.get('/' + account + '/' + application + '/_admin/_assetsstorage?x-token=' + xToken)
            .then(
            function (response) {
                self.pending = false;
                self.alerts = [];
                if (response.data != null) {
                    self.selectBranch = {
                        model: null,
                        availableOptions: []
                    }
                    for (var i = 0; i < response.data.length; i++) {
                        var item = { Name: response.data[i].BranchName, Path: response.data[i].BranchPath };
                        self.selectBranch.availableOptions.push(item);
                    }
                }
            }, function (response) {
                self.pending = false;
                self.alerts = [];
                self.alerts.push({ type: 'danger', message: response.data || response.statusText });
                self.selectBranch = {
                    model: null,
                    availableOptions: []
                }
            }
            );
    }

    this.removeBranch = function () {
        let branchName = self.selectBranch.model;
        self.pending = true;
        if (branchName != null) {
            $http.delete('/' + account + '/' + application + '/_admin/_assetsstorage/' + branchName + '?x-token=' + xToken)
                .then(
                function (response) {
                    self.pending = false;
                    self.alerts = [];
                    setTimeout(function () {
                        self.getBranches();
                    }, self.refreshDelay);
                },
                function (response) {
                    self.pending = false;
                    self.alerts = [];
                    self.alerts.push({ type: 'danger', message: response.data || response.statusText });
                    setTimeout(function () {
                        self.getBranches();
                    }, self.refreshDelay);
                }
                );
        }
        else {
            setTimeout(function () {
                self.getBranches();
            }, self.refreshDelay);
            self.pending = false;
            self.alerts = [];
            self.alerts.push({ type: 'danger', message: "Please select branch" });
        }
    }

    /******************* Popup *******************/
    this.showBranchPopUp = function () {
        var modalInstance = $uibModal.open({
            animation: false,
            templateUrl: 'ModalCreateBranch.html',
            controller: "CreateBranchPopup",
            controllerAs: "$CreateBranchPopup",
            resolve:
            {
                // Add all branches
                datas: function () {
                    return { account: account, application: application, xToken: xToken, branches: self.selectBranch.availableOptions }
                }
            }
        });

        //Refresh list of branches 
        modalInstance.result.then(function () {
            self.pending = true;
            setTimeout(function () {
                self.pending = false;
                self.getBranches();
            }, self.refreshDelay);

        }, function () {

        });
    }

    this.showFilePopup = function () {
        self.alerts = [];
        if (self.selectBranch.model == null) {
            self.alerts.push({ type: 'danger', message: "Please select branch" });
            return;
        }
        var modalInstance = $uibModal.open({
            animation: false,
            templateUrl: 'ModalCreateFile.html',
            controller: "CreateFilePopup",
            controllerAs: "$CreateFilePopup",
            backdrop: 'static',
            keyboard: false,
            resolve:
            {
                datas: function () {
                    return {
                        account: account,
                        application: application,
                        xToken: xToken,
                        activeBranch: self.selectBranch.model
                    }
                }
            }
        });

        modalInstance.result
            .then(
            function () {
                self.pending = true;
                setTimeout(function () {
                    self.pending = false;
                    self.getBranch(self.selectBranch.model);
                }, self.refreshDelay);
            },
            function () {

            }
            );
    }

    this.showModalInformation = function () {
        var modalInstance = $uibModal.open({
            animation: false,
            templateUrl: 'ModalInformation.html',
            controller: "InformationPopup",
            controllerAs: "$InformationPopup",
            resolve:
            {
                datas: function () {
                    return { title: self.modalTitle, message: self.modalMessage }
                }
            }
        });
    }

    this.showRemoveBranchPopup = function () {
        var modalInstance = $uibModal.open({
            animation: false,
            templateUrl: 'ConfirmationPopup.html',
            controller: "ConfirmationPopup",
            controllerAs: "$ConfirmationPopup",
            resolve:
            {
                datas: function () {
                    return {
                        title: "Delete confirmation",
                        message: "Are you sure you want to delete branch " + self.selectBranch.model + " ?"
                    }
                }
            }
        });

        modalInstance.result
            .then(
            function () {
                self.removeBranch();
            },
            function () { }
            );
    }

    this.showRemoveFilePopup = function (path, fileName) {
        var modalInstance = $uibModal.open({
            animation: false,
            templateUrl: 'ConfirmationPopup.html',
            controller: "ConfirmationPopup",
            controllerAs: "$ConfirmationPopup",
            resolve:
            {
                datas: function () {
                    return {
                        title: "Delete confirmation",
                        message: "Are you sure you want to delete file " + path + fileName + " ?"
                    }
                }
            }
        });

        modalInstance.result
            .then(
            function () {
                self.removeFile(path, fileName);
            },
            function () { }
            );
    }

    /******************* Events *******************/
    // Call when download file succeed.
    this.OnSuccessDownload = function (response, pfileName) {
        var contentType = response.headers("Content-Type");        
        var fileName = pfileName;
        var file = new Blob([response.data], {
            type: contentType
        });
        var fileURL = URL.createObjectURL(file);
        document.getElementById("dlLink").href = fileURL;
        document.getElementById("dlLink").download = fileName;
        var obj = document.getElementById("dlLink");
        obj.click();
        self.pending = false;
    }

    // Call when download file failed
    this.OnFaildedModal = function (response) {
        self.modalTitle = "Failed to download file";
        self.modalMessage = response.data;
        self.showModalInformation();
        self.pending = false;
    }

    this.getBranches();
});

assetsStorage.component('assets', {
    templateUrl: 'files-list.template.html',
    controller: "assetsStorageController"
});

angular.bootstrap(document, ["assetsStorageApp"]);