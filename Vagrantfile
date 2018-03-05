# -*- mode: ruby -*-
# vi: set ft=ruby :

Vagrant.configure(2) do |config|

  config.vm.box = "bento/ubuntu-16.04"
 
  config.vm.provider "virtualbox" do |v, override|
    v.customize ["modifyvm", :id, "--natdnshostresolver1", "on"]
  end

  config.vm.provider "libvirt" do |v, override|
    override.vm.box = "stephenpearson/ubuntu-16.04"
  end

  config.vm.provider "virtualbox" do |v|
    v.memory = 2048
    v.cpus = 2
  end

  config.vm.network "forwarded_port", guest: 24444, host: 24444
  config.vm.network "forwarded_port", guest: 5001,  host: 5001

  config.vm.provision "ansible" do |ansible|
    ansible.extra_vars = {
      DEPLOY_TYPE: "test",
      vagrant: true
    }
    ansible.playbook = "deploy.yml"
    ansible.verbose = "vvvv"
  end
end
