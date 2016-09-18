var jqTreeViewInstances = [];
var jqDestinationTreeView = null;
var jqSourceTreeView = null;

;(function($, window, document, undefined) {
"use strict";
	var pluginName = 'jqTreeView',
        defaults = {
        	id: "",
        	dataUrl: "",
        	dragAndDropUrl: "",
        	nodes: [],
        	hoverOnMouseOver: true,
        	checkBoxes: false,
        	multipleSelect: false,
        	nodeTemplateID: "",
        	dragAndDrop: false,
        	width: 300,
        	height: 500,
        	onExpand: null,
        	onCollapse: null,
        	onCheck: null,
        	onSelect: null,
        	onMouseOver: null,
        	onMouseOut: null,
        	onKeyDown: null,
        	onNodesDragged: null,
        	onNodesMoved: null,
        	onNodesDropped: null
        },
        toggleActive = false;

	function TreeView(element, options) {
		jqTreeViewInstances[jqTreeViewInstances.length] = this;
		this.self = $(element);		
		this.options = $.extend({}, defaults, options);
		this.accessLink = $("#" + options.id + "_accessLink");
		this.toggleActive = toggleActive;
		this.dragDiv = null;
		this.dropHint = null;		
		this.hoveredNode = null;
		this.focusedNode = null;
		this.draggedNodes = [];
		this.leftMouseButtonPressed = false;
		
		$(element).prop("treeview", this);
		this.self.addClass("ui-widget ui-widget-content ui-jqtreeview");
		
		if (this.options.nodes.length > 0) {			
			this.nodesLoaded(this.options.nodes);
		}
		else {
			this.loadNodes();
		}
	};

	TreeView.prototype.loadNodes = function() {		
		$.ajax({
			url: this.options.dataUrl,
			type: "GET",
			success: this.executeInContext(this, this.nodesLoaded)
		});
	};
	
	TreeView.prototype.getUrlAppendChar = function(s) {
		if (s.indexOf("?") !== -1) {
			return "&";
		}
		return "?";
	};
	
	TreeView.prototype.loadChildNodes = function(parentNodeValue, childElement, parentElement) {
		var that = this;
		var url = this.options.dataUrl + this.getUrlAppendChar(this.options.dataUrl);
		url = url + "parentNodeValue=" + encodeURIComponent(parentNodeValue);
		url = url + "&clientID=" + encodeURIComponent(this.options.id);		
		var startElement = childElement ? childElement : this.self;

		$.ajax({
			url: url,
			type: "GET",
			success: function(json) {
				that.renderNodes($.parseJSON(json), startElement);
				that.serializeCheckedState();
				that.serializeSelectedState();
				that.serializeExpandedState();
				parentElement.trigger("expand");
			}
		});
	};	

	TreeView.prototype.executeInContext = function(context, func) {
		return function() {
			func.apply(context, arguments);
		};
	};

	TreeView.prototype.nodesLoaded = function(json) {		
		var parsedJson = json;
		if (!$.isArray(json))		
			parsedJson = $.parseJSON(json)
		this.renderNodes(parsedJson, this.self);
		this.renderHiddenStateFields();
		this.attachEvents();
		this.serializeCheckedState();
		this.serializeSelectedState();
		this.serializeExpandedState();
	};
	
	TreeView.prototype.renderNodes = function(json, parent) {		
		var that = this;
		parent.empty();
		$.each(json, function(index, options) {
			that.renderNode(options, parent);
		});
	};
	
	TreeView.prototype.renderNode = function(nodeOptions, parent) {
		var nodeEnabled = true;
		if (typeof nodeOptions.enabled == "undefined")
			nodeEnabled = true;
		else if (!nodeOptions.enabled)
			nodeEnabled = false;

		var templateOptions = $.extend({
			treeView: this,
			treeViewID: this.options.id,
			expanded: nodeOptions.expanded ? true : false,
			selected: nodeOptions.selected ? true : false,
			enabled: nodeEnabled,
			loadOnDemand: nodeOptions.loadOnDemand ? true : false,
			expandedLiClass: nodeOptions.expanded ? "ui-jqtreeview-item-expanded" : "",
			url: nodeOptions.url ? "href=" + nodeOptions.url : "",
			tabindex: nodeOptions.selected ? 0 : -1,
			itemCssClass: this.getItemCssClass(nodeOptions),
			expandImageCssClass: this.getExpandImageCssClass(nodeOptions),
			initialDisplay: nodeOptions.expanded ? "block" : "none",
			initialImage: this.getCurrentNodeImage(nodeOptions),
			checkBoxes: this.options.checkBoxes,
			initialCheckedState: nodeOptions.checked ? "checked" : "",
			selectedCssClass: nodeOptions.selected ? "ui-state-highlight" : "",
			disabledItemTextClass: nodeEnabled ? "" : "ui-jqtreeview-item-text-disabled",
			showExpandImage: nodeOptions.nodes || nodeOptions.loadOnDemand,
			hovered: false	}, nodeOptions);
		
		
		var nodeContent = this.getNodeContent(nodeOptions);		
		var newNode = $(this.renderNodeHtml(templateOptions, nodeContent));
	
		var newParent = parent
							.append(newNode)
							.find("ul:last");
		newNode.data("options", templateOptions);

		if (nodeOptions.nodes) {
			this.renderNodes(nodeOptions.nodes, newParent);
		}		
	}; // end of doRenderNode 			
	
	TreeView.prototype.getNodeContent = function(templateOptions) {
		var text = templateOptions.text;
		var templateID = "";		

		if (this.options.nodeTemplateID) {
			templateID = this.options.nodeTemplateID;
		}
		if (templateOptions.nodeTemplateID) {
			templateID = templateOptions.nodeTemplateID;
		}
		if (templateID) {
			text = this.renderTemplate(templateID, templatedOptions);
		}
		
		return text;
	};
	
	TreeView.prototype.renderNodeHtml = function(t, text) {
		var a = [];
		a[a.length] = "<li class='ui-jqtreeview-item " + t.expandedLiClass + "'>";
		a[a.length] = "<a tabindex=" + t.tabindex + " " + t.url + " class='" + t.itemCssClass + "' onfocus='this.blur();'>";
		a[a.length] = "<table cellpadding=0 cellspacing=0 class='ui-helper-reset'>";
		a[a.length] = "<tr class='ui-jqtreeview-aref " + t.selectedCssClass + "'>";
		if (t.showExpandImage)
			a[a.length] = "<td valign=middle style='width:0px !important'><span class='ui-icon ui-jqtreeview-item-expand-image " + t.expandImageCssClass + "'></span></td>";
		if (t.checkBoxes)
			a[a.length] = "<td valign=middle style='width:21px;text-align:center;'><input type='checkbox' class='ui-jqtreeview-item-checkbox' " + t.initialCheckedState + "></input></td>";
		if (t.imageUrl)
			a[a.length] = "<td valign=middle style='width:21px;text-align:center;'><img class='ui-jqtreeview-item-image' src='" + t.initialImage + "' /></td>";
		a[a.length] = "<td valign=middle><span class='ui-jqtreeview-item-text " + t.disabledItemTextClass + "'>" + text +"</span></td>";
		a[a.length] = "</tr></table></a>";
		if (t.showExpandImage)
			a[a.length] = "<ul style='display:" + t.initialDisplay + "'></ul>";
		a[a.length] = "</li>";	
		
		return a.join("");
	}
	
	TreeView.prototype.getItemCssClass = function(options) {	
		var result = "";
		result = result.concat(" ui-corner-all ui-jqtreeview-aref");
		if (options.nodes) {
			result = result.concat(" ui-jqtreeview-parent");
			if (!options.expanded)
				result = result.concat(" ui-jqtreeview-parent-collapsed");
		}
		return result;
	};
	
	TreeView.prototype.renderTemplate = function(templateID, json) {
		if (!$.isFunction($.tmpl)) {
			alert("You have specified using templates with jqTreeView, but the jQuery template library javascript is not referenced in the HTML.");
			return;
		}
		if ($("#" + templateID).length === 0) {
			alert("You have not specified a jQuery template with ID " + templateID + " that is not defined on the page");
			return;
		}
		
		if (json) {
			return $("#" + templateID).tmpl(json).clone().wrap('<div></div>').parent().html();
		}
		
		return $("#" + templateID).tmpl().clone().wrap('<div></div>').parent().html();
	};	

	TreeView.prototype.renderHiddenStateFields = function() {
		var checkedID = this.getCheckedStateHiddenID();
		var selectedID = this.getSelectedStateHiddenID();
		var expandedID = this.getExpandedStateHiddenID();
		this.self.remove("#" + checkedID).append("<input type='hidden' id='" + checkedID + "' name='" + checkedID + "'></input>");
		this.self.remove("#" + selectedID).append("<input type='hidden' id='" + selectedID + "' name='" + selectedID + "'></input>");
		this.self.remove("#" + expandedID).append("<input type='hidden' id='" + expandedID + "' name='" + expandedID + "'></input>");
	};

	TreeView.prototype.getCheckedStateHiddenID = function() {
		return this.options.id + "_checkedState";
	};

	TreeView.prototype.getSelectedStateHiddenID = function() {
		return this.options.id + "_selectedState";
	};

	TreeView.prototype.getExpandedStateHiddenID = function() {
		return this.options.id + "_expandedState";
	};

	TreeView.prototype.serializeCheckedState = function() {
		var nodes = [];
		var that = this;
		$.each(this.self.find("input:checked"), function(i, checkbox) {
			nodes[nodes.length] = that.serializeNode(checkbox);
		});

		$("#" + this.getCheckedStateHiddenID()).val(JSON.stringify(nodes));
	};

	TreeView.prototype.serializeSelectedState = function() {
		var nodes = [];
		var that = this;
		$.each(this.self.find(".ui-state-highlight"), function(i, node) {
			nodes[nodes.length] = that.serializeNode(node);
		});

		$("#" + this.getSelectedStateHiddenID()).val(JSON.stringify(nodes));
	};

	TreeView.prototype.serializeExpandedState = function() {
		var nodes = [];
		var that = this;
		$.each(this.self.find(".ui-jqtreeview-item-expanded"), function(i, node) {
			nodes[nodes.length] = that.serializeNode(node);
		});

		$("#" + this.getExpandedStateHiddenID()).val(JSON.stringify(nodes));
	};

	TreeView.prototype.serializeNode = function(element) {		
		var options = this.getNodeOptions($(element));
		var node = new Object();
		if (options.text) node.text = options.text;
		if (options.value) node.value = options.value;
		if (options.checked) node.checked = true;
		if ($(element).hasClass("ui-state-highlight")) node.selected = true;
		node.treeViewID = options.treeViewID;

		return node;
	};

	TreeView.prototype.getNodeOptions = function(target) {
		return this.getNodeParentLiElement(target).data("options");
	};

	TreeView.prototype.getNodeParentLiElement = function(target) {
		return target.hasClass("ui-jqtreeview-item") ? target : target.parents("li:eq(0)");
	};
	
	TreeView.prototype.getNodeTableElement = function(target) {
		return this.getNodeParentLiElement(target).find("table:eq(0)");
	};

	TreeView.prototype.getNodeImageElement = function(target) {
		return this.getNodeParentLiElement(target).find(".ui-jqtreeview-item-image:eq(0)");
	};

	TreeView.prototype.getNodeCheckBoxElement = function(target) {
		return this.getNodeParentLiElement(target).find(".ui-jqtreeview-item-checkbox:eq(0)");
	};

	TreeView.prototype.getNodeTextElement = function(target) {
		return this.getNodeParentLiElement(target).find(".ui-jqtreeview-item-text:eq(0)");
	};

	TreeView.prototype.getNodeExpandImageElement = function(target) {
		return this.getNodeParentLiElement(target).find(".ui-jqtreeview-item-expand-image:eq(0)");
	};

	TreeView.prototype.getNodeChildUlElement = function(target) {
		return target.parents("a:eq(0)").next();
	};

	TreeView.prototype.getExpandImageCssClass = function(options) {
		return options.expanded ? "ui-icon-circlesmall-minus large" : "ui-icon-circlesmall-plus large";
	};

	TreeView.prototype.getCurrentNodeImage = function(options) {
		if (options.expandedImageUrl && options.expanded)
			return options.expandedImageUrl;
		if (options.imageUrl)
			return options.imageUrl;
	};

	TreeView.prototype.attachEvents = function() {
		this.self
			.bind('mouseover', this.executeInContext(this, this.handleMouseOver))
			.bind('mouseout', this.executeInContext(this, this.handleMouseOut))
			.bind('expand', this.executeInContext(this, this.handleExpand))
			.bind('collapse', this.executeInContext(this, this.handleCollapse))
			.bind('toggle', this.executeInContext(this, this.handleToggle))
			.bind('click', this.executeInContext(this, this.handleClick))			
			.bind('mousedown', this.executeInContext(this, this.handleMouseDown));
		this.accessLink
			.bind('focus', this.executeInContext(this, this.handleFocus))
			.bind('blur', this.executeInContext(this, this.handleBlur))
			.bind('keydown', this.executeInContext(this, this.handleKeyDown));			
					
		$(document)
			.bind('mousemove', this.executeInContext(this, this.handleMouseMove))
			.bind('mouseup', this.executeInContext(this, this.handleMouseUp));
	};
	
	TreeView.prototype.handleFocus = function(event) {				
		var node = this.getFocusedNode();						
		this.focusNode(node);
	};	
	
	TreeView.prototype.handleBlur = function(event) {		
		var savedNode = this.focusedNode;
		this.unfocusNode(this.focusedNode);
		this.focusedNode = savedNode;
	};	
	
	TreeView.prototype.focusNode = function(node) {
		var options = this.getNodeOptions(node);			
		var row = node.find("tr.ui-jqtreeview-aref:first");		
		
		if (row.length && !row.hasClass("ui-state-focus")) {
			row.addClass("ui-state-focus")
				.addClass("ui-jqtreeview-focus");
			options.focused = true;
			this.focusedNode = node;
		}
	};
	
	TreeView.prototype.unfocusNode = function(node) {
		var options = this.getNodeOptions(node);
		var row = node.find("tr.ui-jqtreeview-aref:first");
		if (row.length && row.hasClass("ui-state-focus")) {
			row.removeClass("ui-state-focus")
				.removeClass("ui-jqtreeview-focus");
				
			options.focused = false;
			this.focusedNode = null;
		}
	};	
	
	TreeView.prototype.handleKeyDown = function(event) {		
		// 38 up, 40 down, 39 right, 37 left		
		
		if (this.options.onKeyDown) {
			var result = this.options.onKeyDown(this, event);
			if (result === false) {
				return;
			}
		}	
		
		var oldFocusedNode = this.focusedNode;
		var newFocusedNode;
		
		switch (event.keyCode) {
			case 40: // down
				newFocusedNode = this.getNextActiveChildNode(this.focusedNode);				
				if (!newFocusedNode)
					newFocusedNode = this.getNextActiveSiblingNode(this.focusedNode);	
				if (!newFocusedNode)
					newFocusedNode = this.getNextActiveNode(this.focusedNode);
				if (newFocusedNode) {
					this.unfocusNode(oldFocusedNode);
					this.focusNode(newFocusedNode);
				}
				break;
			case 38: // up								
				newFocusedNode = this.getPrevActiveSiblingNode(this.focusedNode);					
				if (newFocusedNode)
					newFocusedNode = this.getLastVisibleNode(newFocusedNode);				
				if (!newFocusedNode)
					newFocusedNode = this.getActiveParentNode(this.focusedNode);		
				
				break;
			case 13: // enter				
				if (!(this.options.multipleSelect && event.ctrlKey))
					this.unSelectAll();				
				this.select(this.getNodeTextElement(this.focusedNode), event);		
				break;
			case 32: // space				
				if (this.options.checkBoxes && this.focusedNode) {
					
					var options = this.getNodeOptions(this.focusedNode);				
					var checkBox = this.getNodeCheckBoxElement(this.focusedNode);				
					options.checked = !options.checked;					
					checkBox.prop('checked', options.checked);							
					
					if (this.options.onCheck) {						
						var result = this.options.onCheck(node, event);
						if (result === false) {
							return false;
						}
					}
					
					this.serializeCheckedState();
				}
				break;							
			case 39: // right-arrow
			case 187: // +
				if (	this.focusedNode && 
						this.focusedNode.children("ul").children("li").length > 0 && 
						this.getNodeOptions(this.focusedNode).expanded === false ) {
						
					this.handleToggle(event, this.focusedNode);													
				}
				break;
			case 37: // left-arrow
			case 189: // -
				if (	this.focusedNode && 
						this.focusedNode.children("ul").children("li").length > 0 && 
						this.getNodeOptions(this.focusedNode).expanded === true ) {
						
					this.handleToggle(event, this.focusedNode);													
				}
				break;
		}	
		
		if (newFocusedNode) {
			this.unfocusNode(oldFocusedNode);				
			this.focusNode(newFocusedNode);
			event.preventDefault();
			return false;				
		}		
	};
	
	TreeView.prototype.getLastVisibleNode = function(node) {	
		if (node.children("ul").children("li").length && !this.getNodeOptions(node).expanded)
			return node;
	
		var currentNode = node;
		while (1) {
			var childNodes = currentNode.children("ul").children("li");
			
			if (!childNodes.length)
				return currentNode;			
			
			if (!childNodes.length) return			
			
			for (var i=0; i<childNodes.length; i++) {
				var tempNode = childNodes.eq(i);
				if (this.getNodeOptions(tempNode).enabled)
					currentNode = tempNode;
			}
		}
	};
	
	TreeView.prototype.getFocusedNode = function() {
		var nextNode = null;
		if (!this.focusedNode) 
			return this.self.find("li:first");	
			
		return this.focusedNode;
	};
	
	TreeView.prototype.getNextActiveChildNode = function(node) {
		var options = this.getNodeOptions(node);
		if (options.expanded) {
			var childNodes = node.children("ul").children("li");			
			for (var i=0; i<childNodes.length; i++) {				
				var childNodeOptions = this.getNodeOptions(childNodes.eq(i));				
				if (childNodeOptions.enabled) 
					return childNodes.eq(i);
			}
		}		
		
		return null;
	};
	
	TreeView.prototype.getActiveParentNode = function(node) {
		var parentNode = node.closest("ul").closest("li");
		if (parentNode.length)
			return parentNode;
			
		return null;
	};
	
	TreeView.prototype.getNextActiveSiblingNode = function(node) {
		var nextNode = node.next();
		while (nextNode.length > 0 && nextNode.prop("tagName") == "LI") {			
			var options = this.getNodeOptions(nextNode);
			if (options.enabled)
				return nextNode;
				
			nextNode = node.next();
		}		
		
		return null;
	};
	
	TreeView.prototype.getPrevActiveSiblingNode = function(node) {
		var prevNode = node.prev();
		while (prevNode.length > 0 && prevNode.prop("tagName") == "LI") {			
			var options = this.getNodeOptions(prevNode);
			if (options.enabled)
				return prevNode;
				
			prevNode = node.prevNode();
		}		
		
		return null;
	};
	
	TreeView.prototype.getPrevActiveNode = function(node) {
	}
	
	TreeView.prototype.getNextActiveNode = function(node) {
		var parentNode = node.closest("ul").closest("li");		
		
		while (parentNode.length) {
			var nextNode = this.getNextActiveSiblingNode(parentNode);
			if (nextNode)
				return nextNode;
			
			parentNode = parentNode.closest("ul").closest("li");			
		}	

		return null;
	};

	TreeView.prototype.handleMouseOver = function(event) {
		var target = $(event.target);	
		if (target.hasClass("ui-jqtreeview-item-expand-image")) return;		
		var node = this.getNodeParentLiElement(target);		
		var nodeOptions = this.getNodeOptions(target);		

		if (this.options.hoverOnMouseOver && nodeOptions.enabled) {
			var row = $(target).parents("tr.ui-jqtreeview-aref:first");
			if (row.length && !row.hasClass("ui-state-hover")) {
					row.addClass("ui-state-hover")
						.addClass("ui-jqtreeview-hover");
				
					nodeOptions.hovered = true;
					this.hoveredNode = node;
			}
		}
		if (this.options.dragAndDrop && this.dragDiv && target.hasClass("ui-jqtreeview-aref")) {			
			var tableRow = node.find("tr.ui-jqtreeview-aref:first");		
			var offset = tableRow.offset();			
				
			//if (offset.top > event.pageY) {				
				//this.dropHint = this.createDropHint(offset.left, event.pageY, tableRow.width());
			//}
		}
		
		if (this.options.onMouseOver) {			
			this.options.onMouseOver(node, event);
		}		
	};

	TreeView.prototype.handleMouseOut = function(event) {
		var target = $(event.target);
		var nodeOptions = this.getNodeOptions(target);
		nodeOptions.hovered = false;
		this.hoveredNode = null;

		if (this.options.hoverOnMouseOver) {
			this.self.find(".ui-state-hover").removeClass("ui-state-hover");
		}		
		//this.destroyDropHint();
		
		if (this.options.onMouseOut) {
			var node = this.getNodeParentLiElement(target);
			this.options.onMouseOut(node, event);
		}		
	};

	TreeView.prototype.handleExpand = function(event) {
		if (!this.toggleActive) {
			var target = $(event.target);
			var options = this.getNodeOptions(target);

			if (options.enabled) {
				options.expanded = true;


				if (this.options.onExpand) {
					var node = this.getNodeParentLiElement(target);
					var result = this.options.onExpand(node, event);
					if (result === false) {
						options.expanded = true;
						return;
					}
				}

				this.toggleActive = true;
				this.getNodeParentLiElement(target).addClass("ui-jqtreeview-item-expanded");
				target.removeClass("ui-icon-circlesmall-plus large").addClass("ui-icon-circlesmall-minus large");
				this.getNodeImageElement(target).attr("src", this.getCurrentNodeImage(options));
				this.getNodeChildUlElement(target).hide().slideDown(120, this.executeInContext(this, this.toggleEnded));
				this.serializeExpandedState();
			}
		}
	};

	TreeView.prototype.handleCollapse = function(event) {
		if (!this.toggleActive) {
			var target = $(event.target);
			var options = this.getNodeOptions(target);

			if (options.enabled) {
				options.expanded = false;

				if (this.options.onCollapse) {
					var node = this.getNodeParentLiElement(target);
					var result = this.options.onCollapse(node, event);
					if (result === false) {
						options.expanded = true;
						return;
					}
				}

				this.toggleActive = true;
				this.getNodeParentLiElement(target).removeClass("ui-jqtreeview-item-expanded");
				target.removeClass("ui-icon-circlesmall-minus").addClass("ui-icon-circlesmall-plus large");
				this.getNodeImageElement(target).attr("src", this.getCurrentNodeImage(options));

				this.getNodeChildUlElement(target).slideUp(120, this.executeInContext(this, this.toggleEnded));
				this.serializeExpandedState();
			}
		}
	};

	TreeView.prototype.toggleEnded = function() {
		this.toggleActive = false;
	};

	TreeView.prototype.handleToggle = function(event, node) {
		var target = node ? this.getNodeExpandImageElement(node) : $(event.target);		
		var nodeOptions = this.getNodeOptions(target);
		var expanded = nodeOptions.expanded;

		if (nodeOptions.loadOnDemand && !expanded) {
			var childElement = this.getNodeChildUlElement(target);
			this.loadChildNodes(nodeOptions.value, childElement, target);
			return;
		}

		expanded ? target.trigger('collapse') : target.trigger('expand');
	};

	TreeView.prototype.handleClick = function(event) {		
		var target = $(event.target);
		var node = this.getNodeParentLiElement(target);
		var nodeOptions = this.getNodeOptions(target);

		if (target.hasClass("ui-jqtreeview-item-checkbox")) {
			this.accessLink.focus();
			nodeOptions.checked = target.prop("checked");
			if (this.options.onCheck) {
				var result = this.options.onCheck(node, event);
				if (result === false) {
					return false;
				}
			}			
			this.serializeCheckedState();
			return;
		}
		if (target.hasClass('ui-jqtreeview-item-expand-image')) {			
			this.handleToggle(event);
			return false;
		}

		if (!(this.options.multipleSelect && event.ctrlKey)) {
			this.unSelectAll();
		}
		
		this.accessLink.focus();
		this.select(target, event);
	};
	
	TreeView.prototype.handleMouseDown = function(event) {
		var target = $(event.target);
		var node = this.getNodeParentLiElement(target);
		var nodeTable = this.getNodeTableElement(target);
		var nodeOptions = this.getNodeOptions(target);
	
		if (node && this.options.dragAndDrop && event.which == 1) {
			this.leftMouseButtonPressed = true;			
			
			window.setTimeout(
				this.executeInContext(this, 
					function(e) {
						if (this.leftMouseButtonPressed) {
							//this.dragDiv = this.cloneSelectedNodes(nodeTable, nodeOptions, event);			
							this.draggedNodes = [];
							
							var selector = this.self.find(".ui-state-hover");
							if (selector.length === 0 || selector.is(".ui-state-highlight")) {
								selector = this.self.find(".ui-state-highlight");
							}
							
							$.each(selector, 
								this.executeInContext(this, 
									function(i, node) {														
										var clonedNode = $(node).parents("li:eq(0)");
										this.draggedNodes.push(clonedNode);
									}));							
							
							this.dragDiv = this.cloneSelectedNodes(this.draggedNodes, event);										
									
							if (this.options.onNodesDragged) {
								var eventArgs = new Object();
								eventArgs.draggedNodes = this.draggedNodes;
								eventArgs.sourceTreeView = this;								
								
								this.options.onNodesDragged(eventArgs, event);
							}							
							
							jqSourceTreeView = this;
							return false;
						}
					}
				), 150);				
				
				return false;
		}
	};
	
	TreeView.prototype.handleMouseMove = function(event) {
		if (this.options.dragAndDrop && this.dragDiv) {
			this.dragDiv.css({		
										top: event.pageY + 2,
										left: event.pageX + 2 
									});
			
			if (this.options.onNodesMoved) {
				var eventArgs = new Object();
				eventArgs.movedNodes = this.draggedNodes;
				eventArgs.sourceTreeView = this;
				
				this.options.onNodesMoved(eventArgs, event);
			}			
			event.preventDefault();
			return false;
		}
	};
	
	TreeView.prototype.handleMouseUp = function(event) {
		this.leftMouseButtonPressed = false;
		
		if (this.options.dragAndDrop && this.dragDiv) {
			this.dragDiv.remove();
			this.dragDiv = null;
			
			var destNode = this.getDestinationNode();
			//this.destroyDropHint();
			if (this.options.onNodesDropped) {				
				var eventArgs = new Object();
				eventArgs.draggedNodes = this.draggedNodes;
				eventArgs.sourceTreeView = jqSourceTreeView; // not really!
				eventArgs.destinationNode = destNode;
				eventArgs.destinationTreeView = jqDestinationTreeView;
		
				var result = this.options.onNodesDropped(eventArgs, event);
				if (result === false)
					return;
			}
						
			if (destNode) {				
				if (this.draggedNodes.length > 0) {										
				
					for (var i=0; i<this.draggedNodes.length; i++) {
						var draggedNode = this.draggedNodes[i];
						var draggedNodeOptions = this.getNodeOptions(draggedNode);	
						var destNodeOptions = this.getNodeOptions(destNode);
					
						if (!draggedNode.is(destNode) && (draggedNode.has(destNode).length == 0)) {
							var appendToNode = destNode.children().last();						
							var ul = $("<ul />").prepend(draggedNode);					
							appendToNode.after(ul);
						}								
					}
					
					if (this.options.dragAndDropUrl) {
						var args = new Object();
						args.draggedNodes = [];
						
						for (var i=0; i<this.draggedNodes.length; i++) {
							args.draggedNodes.push(this.serializeNode(this.draggedNodes[i]));
						}
						args.destinationTreeViewID = jqDestinationTreeView.options.id;
						args.sourceTreeViewID = jqSourceTreeView.options.id;
						args.destinationNode = this.serializeNode(destNode);
						
						var argsData = JSON.stringify(args);						
						
						var url = this.options.dragAndDropUrl + this.getUrlAppendChar(this.options.dragAndDropUrl);
						url = url + "&clientID=" + encodeURIComponent(this.options.id);
						url = url + "&jqTreeViewNodesDropped=true";
										
						$.ajax({
							url: url,
							type: "GET",
							contentType: 'application/json; charset=utf-8',							
							data: { args: argsData }
						});
					}
				}
			}
		}
	};
	
	TreeView.prototype.getSourceTreeView = function() {
		for (var i=0; i<jqTreeViewInstances.length; i++) {
			var treeView = jqTreeViewInstances[i];
			if (treeView.draggedNodes.length) return treeView;
		}
		
		return null;
	};
	
	TreeView.prototype.createDropHint = function(x, y, width) {
		this.dropHint = $("<div id='jqTreeViewDropHint' class='ui-jqtreeview-drop-hint' />")
								.css({ 
									visibility: "visible",
									top: y,
									left: x,		
									width: width							
								});
								
								
		$(document.body).append(this.dropHint);
		return this.dropHint;
	};
	
	TreeView.prototype.destroyDropHint = function() {
		$("#jqTreeViewDropHint").remove();
		this.dropHint = null;
	};
	
	//TreeView.prototype.cloneSelectedNodes = function(node, nodeOptions, event) {
	TreeView.prototype.cloneSelectedNodes = function(draggedNodes, event) {
			var dragDiv = $("<div />")
										.css({
											padding: 0,
											margin: 0,											
											position: "absolute",
											zIndex: 1000,
											top: event.pageY + 2,
											left: event.pageX + 2,
										})
										.addClass("ui-widget-content ui-corner-all");
			var dragUl = $("<ul class='ui-widget ui-widget-content ui-jqtreeview'>");
			dragDiv.append(dragUl);
			
			for (var i=0; i<draggedNodes.length; i++) {
				var node = draggedNodes[i];
				var nodeOptions = this.getNodeOptions(node);					
				
				var nodeContent = this.getNodeContent(nodeOptions);		
				var clonedNode = $(this.renderNodeHtml(nodeOptions, nodeContent));				
				
				clonedNode.css({padding: "0px 0px 0px 0px"});
				
				var link = clonedNode.find("a");
				link.css({padding: "0px 0px 0px 0px"});				
				
				if (nodeOptions.showExpandImage) {	clonedNode.find("td:eq(0)").remove();	}
				if (nodeOptions.checkBoxes) {	clonedNode.find("td:eq(0)").remove();	}								
				dragUl.append(clonedNode);								
			}

			$(document.body).append(dragDiv);
			
			return dragDiv;
	};
	
	TreeView.prototype.createClonedNpde = function(nodeOptions) {
	};
	
	TreeView.prototype.getDestinationNode = function() {		
		for (var i=0; i<jqTreeViewInstances.length; i++) {
			var treeView = jqTreeViewInstances[i];
			if (treeView.hoveredNode) {
				jqDestinationTreeView = treeView;
				return treeView.hoveredNode;
			}
		}
		return null;
	};

	TreeView.prototype.getNodeByText = function(text) {
		return this.self.find(".ui-jqtreeview-item-text:contains('" + text + "'):eq(0)");
	};

	TreeView.prototype.getNodeByValue = function(value) {
		var that = this;
		that.resultNode = null;
		var nodes = this.self.find(".ui-jqtreeview-item-text");
		$.each(nodes, function(index, node) {
			if (that.getNodeOptions($(node)).value == value) {
				that.resultNode = $(node);
				return false;
			}
		});

		return that.resultNode;
	};

	TreeView.prototype.getAllNodes = function() {
		return this.self.find(".ui-jqtreeview-item");
	};

	TreeView.prototype.check = function(node, event) {
		node = $(node);
		var checkBox = this.getNodeCheckBoxElement(node);
		if (checkBox) {
			this.getNodeOptions(node).checked = true;
			checkBox.prop("checked", true);
			this.serializeCheckedState();
		}
	};

	TreeView.prototype.unCheck = function(node) {
		node = $(node);
		var checkBox = this.getNodeCheckBoxElement(node);
		if (checkBox) {
			this.getNodeOptions(node).checked = false;
			checkBox.prop("checked", false);
			this.serializeCheckedState();
		}
	};

	TreeView.prototype.expand = function(node) {
		node.trigger('expand');
	};

	TreeView.prototype.collapse = function(node) {
		node.trigger('collapse');
	};

	TreeView.prototype.toggle = function(node) {
		node.trigger('toggle');
	};

	TreeView.prototype.expandAll = function() {
		this.self.find(".ui-jqtreeview-item-expand-image").trigger("expand");
	};

	TreeView.prototype.collapseAll = function() {
		this.self.find(".ui-jqtreeview-item-expand-image").trigger("collapse");
	};

	TreeView.prototype.select = function(node, event) {
		var nodeOptions = this.getNodeOptions(node);		
		if (nodeOptions.enabled) {
			if (this.options.onSelect) {
				var result = this.options.onSelect(node, event);
				if (result === false)
					return;
			}

			var row = $(node).parents("tr.ui-jqtreeview-aref:first");
			if (row) {			
				$(row).addClass("ui-state-highlight");
			}
			
			if (this.focusedNode)
				this.unfocusNode(this.focusedNode);			
			this.focusNode(this.getNodeParentLiElement(node));			
			
			this.serializeSelectedState();
		}
	};

	TreeView.prototype.unSelect = function(node) {
		var row = $(node).parents("tr.ui-jqtreeview-aref:first");
		if (row) {
			$(row).removeClass("ui-state-highlight");
		}

		if (this.focusedNodes == node) this.focusedNode = null;
		this.serializeSelectedState();
	};

	TreeView.prototype.unSelectAll = function() {
		this.self.find(".ui-state-highlight").removeClass("ui-state-highlight");

		this.serializeSelectedState();
	};

	$.fn[pluginName] = function(options) {
		return this.each(function() {
			if (!$.data(this, 'plugin_' + pluginName)) {
				$.data(this, 'plugin_' + pluginName, new TreeView(this, options));
			}
		});
	};

	$.fn["getTreeViewInstance"] = function() {
		return $(this).prop("treeview");
	};

})(jQuery, window, document);